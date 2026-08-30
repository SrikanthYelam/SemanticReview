using AI.CodeReview.Application;
using AI.CodeReview.Application.Ai;
using AI.CodeReview.Application.Diffing;
using AI.CodeReview.Application.Git;
using AI.CodeReview.Domain;
using AI.CodeReview.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.CodeReview.Tests.Application;

public class CodeReviewOrchestratorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReviewAsync_EmptyOrWhitespaceDiff_ThrowsArgumentException(string diff)
    {
        var orchestrator = CreateOrchestrator(
            diffParser: new FakeDiffParser(Array.Empty<ParsedFileDiff>()),
            aiReviewer: new FakeAiCodeReviewer());

        await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.ReviewAsync(diff));
    }

    [Fact]
    public async Task ReviewAsync_NonCSharpFiles_AreFilteredOutAndNotAnalyzed()
    {
        var parsed = new List<ParsedFileDiff>
        {
            new("readme.md", new[] { new DiffAddedLine(1, "# Title") }),
            new("src/Foo.cs", new[] { new DiffAddedLine(1, "var x = 1;") })
        };

        var analyzer = new FakeStaticCodeAnalyzer();
        var aiReviewer = new FakeAiCodeReviewer();

        var orchestrator = CreateOrchestrator(new FakeDiffParser(parsed), analyzer, aiReviewer);

        await orchestrator.ReviewAsync("some diff");

        Assert.DoesNotContain("readme.md", analyzer.AnalyzedFileNames);
        Assert.Contains("src/Foo.cs", analyzer.AnalyzedFileNames);
        Assert.DoesNotContain("readme.md", aiReviewer.ReviewedFileNames);
        Assert.Contains("src/Foo.cs", aiReviewer.ReviewedFileNames);
    }

    [Fact]
    public async Task ReviewAsync_NonCSharpAiOnlyReviewableFile_SkipsRoslynButRunsAiReview()
    {
        var parsed = new List<ParsedFileDiff>
        {
            new("src/app.ts", new[] { new DiffAddedLine(1, "const x = 1;") })
        };

        var analyzer = new FakeStaticCodeAnalyzer();
        var aiReviewer = new FakeAiCodeReviewer();

        var orchestrator = CreateOrchestrator(new FakeDiffParser(parsed), analyzer, aiReviewer);

        await orchestrator.ReviewAsync("some diff");

        Assert.DoesNotContain("src/app.ts", analyzer.AnalyzedFileNames);
        Assert.Contains("src/app.ts", aiReviewer.ReviewedFileNames);
    }

    [Fact]
    public async Task ReviewAsync_FileWithNoAddedLines_IsSkipped()
    {
        var parsed = new List<ParsedFileDiff>
        {
            new("src/Untouched.cs", Array.Empty<DiffAddedLine>())
        };

        var analyzer = new FakeStaticCodeAnalyzer();
        var aiReviewer = new FakeAiCodeReviewer();

        var orchestrator = CreateOrchestrator(new FakeDiffParser(parsed), analyzer, aiReviewer);

        var result = await orchestrator.ReviewAsync("some diff");

        Assert.Empty(result.Findings);
        Assert.Empty(analyzer.AnalyzedFileNames);
        Assert.Empty(aiReviewer.ReviewedFileNames);
    }

    [Fact]
    public async Task ReviewAsync_DuplicateFindingAtSameFileLineCategory_KeepsOnlyOne()
    {
        const string file = "src/Foo.cs";
        var parsed = new List<ParsedFileDiff>
        {
            new(file, new[] { new DiffAddedLine(5, "GetTaskAsync().Result;") })
        };

        var roslynFinding = new ReviewFinding(Severity.High, ReviewCategory.Async, file, 1, "Roslyn: .Result usage", "Await instead.", FindingSource.StaticAnalysis);
        var aiDuplicateFinding = new ReviewFinding(Severity.Medium, ReviewCategory.Async, file, 5, "AI: blocking call", "Await instead.", FindingSource.Ai);

        var analyzer = new FakeStaticCodeAnalyzer(new Dictionary<string, IReadOnlyList<ReviewFinding>>
        {
            [file] = [roslynFinding]
        });
        var aiReviewer = new FakeAiCodeReviewer(new Dictionary<string, IReadOnlyList<ReviewFinding>>
        {
            [file] = [aiDuplicateFinding]
        });

        var orchestrator = CreateOrchestrator(new FakeDiffParser(parsed), analyzer, aiReviewer);

        var result = await orchestrator.ReviewAsync("some diff");

        // Roslyn's finding maps local line 1 -> real diff line 5, colliding with the AI finding at line 5/Async.
        var finding = Assert.Single(result.Findings);
        Assert.Equal("Roslyn: .Result usage", finding.Problem);
        Assert.Equal(FindingSource.StaticAnalysis, finding.Source);
    }

    [Fact]
    public async Task ReviewAsync_MultipleFindings_AreSortedBySeverityThenFileThenLine()
    {
        const string fileA = "src/A.cs";
        const string fileB = "src/B.cs";
        var parsed = new List<ParsedFileDiff>
        {
            new(fileA, new[] { new DiffAddedLine(1, "code") }),
            new(fileB, new[] { new DiffAddedLine(1, "code") })
        };

        // Sourced from the AI reviewer fake (not Roslyn) because AI findings carry real line
        // numbers directly, with no local-fragment-to-diff-line remapping applied.
        var lowInA = new ReviewFinding(Severity.Low, ReviewCategory.Maintainability, fileA, 20, "low", "s", FindingSource.Ai);
        var criticalInB = new ReviewFinding(Severity.Critical, ReviewCategory.Security, fileB, 5, "critical", "s", FindingSource.Ai);
        var highInA = new ReviewFinding(Severity.High, ReviewCategory.Bug, fileA, 10, "high", "s", FindingSource.Ai);

        var aiReviewer = new FakeAiCodeReviewer(new Dictionary<string, IReadOnlyList<ReviewFinding>>
        {
            [fileA] = [lowInA, highInA],
            [fileB] = [criticalInB]
        });

        var orchestrator = CreateOrchestrator(new FakeDiffParser(parsed), aiReviewer: aiReviewer);

        var result = await orchestrator.ReviewAsync("some diff");

        Assert.Equal(
            [criticalInB, highInA, lowInA],
            result.Findings);
    }

    [Fact]
    public async Task ReviewAsync_AiReviewerThrows_DegradesGracefullyToRoslynFindingsOnly()
    {
        const string file = "src/Foo.cs";
        var parsed = new List<ParsedFileDiff>
        {
            new(file, new[] { new DiffAddedLine(5, "GetTaskAsync().Result;") })
        };

        var roslynFinding = new ReviewFinding(Severity.High, ReviewCategory.Async, file, 1, "Roslyn: .Result usage", "Await instead.", FindingSource.StaticAnalysis);
        var analyzer = new FakeStaticCodeAnalyzer(new Dictionary<string, IReadOnlyList<ReviewFinding>>
        {
            [file] = [roslynFinding]
        });
        var aiReviewer = new FakeAiCodeReviewer(exceptionToThrow: new AiReviewException("AI provider unavailable"));
        var orchestrator = CreateOrchestrator(new FakeDiffParser(parsed), analyzer, aiReviewer);

        var result = await orchestrator.ReviewAsync("some diff");

        var finding = Assert.Single(result.Findings);
        Assert.Equal("Roslyn: .Result usage", finding.Problem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReviewFromUrlAsync_EmptyOrWhitespaceUrl_ThrowsArgumentException(string gitUrl)
    {
        var orchestrator = CreateOrchestrator(diffParser: new FakeDiffParser(Array.Empty<ParsedFileDiff>()));

        await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.ReviewFromUrlAsync(gitUrl));
    }

    [Fact]
    public async Task ReviewFromUrlAsync_FetchesDiffThenReviewsIt()
    {
        const string file = "src/Foo.cs";
        var parsed = new List<ParsedFileDiff>
        {
            new(file, new[] { new DiffAddedLine(1, "code") })
        };
        var finding = new ReviewFinding(Severity.High, ReviewCategory.Bug, file, 1, "problem", "fix", FindingSource.StaticAnalysis);

        var analyzer = new FakeStaticCodeAnalyzer(new Dictionary<string, IReadOnlyList<ReviewFinding>>
        {
            [file] = [finding]
        });
        var gitDiffFetcher = new FakeGitDiffFetcher(diffToReturn: "fetched diff text");

        var orchestrator = CreateOrchestrator(new FakeDiffParser(parsed), analyzer, gitDiffFetcher: gitDiffFetcher);

        var result = await orchestrator.ReviewFromUrlAsync("https://github.com/owner/repo/commit/abc123");

        Assert.Equal(["https://github.com/owner/repo/commit/abc123"], gitDiffFetcher.RequestedUrls);
        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task ReviewFromUrlAsync_FetcherThrows_ExceptionPropagates()
    {
        // Unlike an AI failure, a failed diff fetch leaves nothing to review at all — it is not
        // caught/degraded by the orchestrator, it propagates to the caller.
        var gitDiffFetcher = new FakeGitDiffFetcher(exceptionToThrow: new GitDiffFetchException("not found"));
        var orchestrator = CreateOrchestrator(
            diffParser: new FakeDiffParser(Array.Empty<ParsedFileDiff>()),
            gitDiffFetcher: gitDiffFetcher);

        await Assert.ThrowsAsync<GitDiffFetchException>(
            () => orchestrator.ReviewFromUrlAsync("https://github.com/owner/repo/commit/abc123"));
    }

    private static CodeReviewOrchestrator CreateOrchestrator(
        FakeDiffParser diffParser,
        FakeStaticCodeAnalyzer? analyzer = null,
        FakeAiCodeReviewer? aiReviewer = null,
        FakeGitDiffFetcher? gitDiffFetcher = null)
        => new(
            diffParser,
            analyzer ?? new FakeStaticCodeAnalyzer(),
            aiReviewer ?? new FakeAiCodeReviewer(),
            gitDiffFetcher ?? new FakeGitDiffFetcher(),
            NullLogger<CodeReviewOrchestrator>.Instance);
}
