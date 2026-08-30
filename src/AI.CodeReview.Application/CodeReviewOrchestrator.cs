using AI.CodeReview.Application.Ai;
using AI.CodeReview.Application.Analysis;
using AI.CodeReview.Application.Diffing;
using AI.CodeReview.Application.Git;
using AI.CodeReview.Domain;
using Microsoft.Extensions.Logging;

namespace AI.CodeReview.Application;

public sealed class CodeReviewOrchestrator : ICodeReviewService
{
    private readonly IDiffParser _diffParser;
    private readonly IStaticCodeAnalyzer _staticCodeAnalyzer;
    private readonly IAiCodeReviewer _aiCodeReviewer;
    private readonly IGitDiffFetcher _gitDiffFetcher;
    private readonly ILogger<CodeReviewOrchestrator> _logger;

    public CodeReviewOrchestrator(
        IDiffParser diffParser,
        IStaticCodeAnalyzer staticCodeAnalyzer,
        IAiCodeReviewer aiCodeReviewer,
        IGitDiffFetcher gitDiffFetcher,
        ILogger<CodeReviewOrchestrator> logger)
    {
        _diffParser = diffParser;
        _staticCodeAnalyzer = staticCodeAnalyzer;
        _aiCodeReviewer = aiCodeReviewer;
        _gitDiffFetcher = gitDiffFetcher;
        _logger = logger;
    }

    public Task<CodeReviewResult> ReviewAsync(string diff, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            throw new ArgumentException("The diff must not be empty.", nameof(diff));
        }

        return ReviewDiffAsync(diff, cancellationToken);
    }

    public async Task<CodeReviewResult> ReviewFromUrlAsync(string gitUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gitUrl))
        {
            throw new ArgumentException("The gitUrl must not be empty.", nameof(gitUrl));
        }

        _logger.LogInformation("Fetching diff from git URL");
        var diff = await _gitDiffFetcher.FetchDiffAsync(gitUrl, cancellationToken);

        return await ReviewDiffAsync(diff, cancellationToken);
    }

    private async Task<CodeReviewResult> ReviewDiffAsync(string diff, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Review started");

        var parsedFiles = _diffParser.Parse(diff);
        var csharpFiles = parsedFiles
            .Where(f => f.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && f.AddedLines.Count > 0)
            .ToList();

        _logger.LogInformation("Diff parsed: {FileCount} C# file(s) with changes", csharpFiles.Count);

        var allFindings = new List<ReviewFinding>();

        foreach (var file in csharpFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reconstructedCode = string.Join('\n', file.AddedLines.Select(l => l.Content));

            var roslynFindings = _staticCodeAnalyzer.Analyze(file.FileName, reconstructedCode);
            var mappedRoslynFindings = roslynFindings.Select(f => MapToRealLineNumber(f, file.AddedLines)).ToList();
            _logger.LogInformation("Roslyn analysis of {FileName} produced {FindingCount} finding(s)", file.FileName, mappedRoslynFindings.Count);
            allFindings.AddRange(mappedRoslynFindings);

            var diffContext = BuildDiffContext(file.AddedLines);
            _logger.LogInformation("AI review started for {FileName}", file.FileName);
            try
            {
                var aiFindings = await _aiCodeReviewer.ReviewAsync(file.FileName, diffContext, cancellationToken);
                _logger.LogInformation("AI review completed for {FileName}: {FindingCount} finding(s)", file.FileName, aiFindings.Count);
                allFindings.AddRange(aiFindings);
            }
            catch (AiReviewException ex)
            {
                // AI is a best-effort enhancement on top of the deterministic Roslyn findings —
                // an unavailable/misconfigured AI provider degrades the review, it doesn't fail it.
                _logger.LogWarning(ex, "AI review failed for {FileName}; continuing with deterministic findings only", file.FileName);
            }
        }

        var findings = allFindings
            .GroupBy(f => (f.File, f.Line, f.Category))
            .Select(g => g.First())
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Line ?? int.MaxValue)
            .ToList();

        _logger.LogInformation("Review completed: {FindingCount} finding(s) after merge/dedupe", findings.Count);

        return new CodeReviewResult(findings);
    }

    private static string BuildDiffContext(IReadOnlyList<DiffAddedLine> addedLines)
        => string.Join('\n', addedLines.Select(l => $"{l.LineNumber}: {l.Content}"));

    private static ReviewFinding MapToRealLineNumber(ReviewFinding finding, IReadOnlyList<DiffAddedLine> addedLines)
    {
        if (finding.Line is not { } localLine || localLine < 1 || localLine > addedLines.Count)
        {
            return finding with { Line = null };
        }

        return finding with { Line = addedLines[localLine - 1].LineNumber };
    }
}
