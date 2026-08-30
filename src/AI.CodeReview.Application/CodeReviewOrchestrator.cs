using AI.CodeReview.Application.Ai;
using AI.CodeReview.Application.Analysis;
using AI.CodeReview.Application.Diffing;
using AI.CodeReview.Application.Git;
using AI.CodeReview.Domain;
using Microsoft.Extensions.Logging;

namespace AI.CodeReview.Application;

public sealed class CodeReviewOrchestrator : ICodeReviewService
{
    // AI-only review (no Roslyn) for common non-C# text/code file types. Extensions not listed
    // here (binaries, lock files, generated output, etc.) are skipped entirely, same as before.
    private static readonly HashSet<string> AiOnlyReviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".go", ".rb", ".php",
        ".razor", ".cshtml", ".sql", ".yml", ".yaml", ".json", ".html", ".css", ".scss"
    };

    // Bounds how many files' AI review calls run concurrently, so a large diff doesn't fan out
    // unboundedly against the AI provider's rate limits.
    private const int MaxConcurrentAiReviews = 4;

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
        var changedFiles = parsedFiles.Where(f => f.AddedLines.Count > 0).ToList();
        var csharpFiles = changedFiles.Where(f => IsCSharpFile(f.FileName)).ToList();
        var aiOnlyFiles = changedFiles.Where(f => !IsCSharpFile(f.FileName) && IsAiOnlyReviewable(f.FileName)).ToList();

        _logger.LogInformation(
            "Diff parsed: {CSharpCount} C# file(s), {OtherCount} other reviewable file(s)",
            csharpFiles.Count, aiOnlyFiles.Count);

        var allFindings = new List<ReviewFinding>();

        foreach (var file in csharpFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reconstructedCode = string.Join('\n', file.AddedLines.Select(l => l.Content));

            var roslynFindings = _staticCodeAnalyzer.Analyze(file.FileName, reconstructedCode);
            var mappedRoslynFindings = roslynFindings.Select(f => MapToRealLineNumber(f, file.AddedLines)).ToList();
            _logger.LogInformation("Roslyn analysis of {FileName} produced {FindingCount} finding(s)", file.FileName, mappedRoslynFindings.Count);
            allFindings.AddRange(mappedRoslynFindings);
        }

        // AI review runs for C# files plus AI-only-reviewable non-C# files, in parallel (bounded)
        // rather than one file at a time, since it's the slow, network-bound step of the pipeline.
        using var aiConcurrencyLimiter = new SemaphoreSlim(MaxConcurrentAiReviews);
        var aiResults = await Task.WhenAll(csharpFiles.Concat(aiOnlyFiles)
            .Select(file => ReviewFileWithAiAsync(file, aiConcurrencyLimiter, cancellationToken)));
        foreach (var result in aiResults)
        {
            allFindings.AddRange(result);
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

    private async Task<IReadOnlyList<ReviewFinding>> ReviewFileWithAiAsync(
        ParsedFileDiff file, SemaphoreSlim concurrencyLimiter, CancellationToken cancellationToken)
    {
        await concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var diffContext = BuildDiffContext(file.AddedLines);
            _logger.LogInformation("AI review started for {FileName}", file.FileName);
            var aiFindings = await _aiCodeReviewer.ReviewAsync(file.FileName, diffContext, cancellationToken);
            _logger.LogInformation("AI review completed for {FileName}: {FindingCount} finding(s)", file.FileName, aiFindings.Count);
            return aiFindings;
        }
        catch (AiReviewException ex)
        {
            // AI is a best-effort enhancement on top of the deterministic Roslyn findings —
            // an unavailable/misconfigured AI provider degrades the review, it doesn't fail it.
            _logger.LogWarning(ex, "AI review failed for {FileName}; continuing with deterministic findings only", file.FileName);
            return Array.Empty<ReviewFinding>();
        }
        finally
        {
            concurrencyLimiter.Release();
        }
    }

    private static bool IsCSharpFile(string fileName)
        => fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsAiOnlyReviewable(string fileName)
        => AiOnlyReviewExtensions.Contains(Path.GetExtension(fileName));

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
