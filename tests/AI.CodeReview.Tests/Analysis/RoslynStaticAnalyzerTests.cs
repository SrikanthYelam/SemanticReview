using AI.CodeReview.Domain;
using AI.CodeReview.Infrastructure.Roslyn;
using Xunit;

namespace AI.CodeReview.Tests.Analysis;

public class RoslynStaticAnalyzerTests
{
    private readonly RoslynStaticAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_ResultUsage_ReturnsAsyncFinding()
    {
        var findings = _analyzer.Analyze("Foo.cs", "var x = GetTaskAsync().Result;");

        var finding = Assert.Single(findings);
        Assert.Equal(ReviewCategory.Async, finding.Category);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal("Foo.cs", finding.File);
    }

    [Fact]
    public void Analyze_WaitUsage_ReturnsAsyncFinding()
    {
        var findings = _analyzer.Analyze("Foo.cs", "GetTaskAsync().Wait();");

        var finding = Assert.Single(findings);
        Assert.Equal(ReviewCategory.Async, finding.Category);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Contains("Wait", finding.Problem);
    }

    [Fact]
    public void Analyze_EmptyCatchBlock_ReturnsExceptionHandlingFinding()
    {
        var code = """
            try
            {
                DoSomething();
            }
            catch (Exception ex)
            {
            }
            """;

        var findings = _analyzer.Analyze("Foo.cs", code);

        var finding = Assert.Single(findings);
        Assert.Equal(ReviewCategory.ExceptionHandling, finding.Category);
        Assert.Equal(Severity.Medium, finding.Severity);
    }

    [Fact]
    public void Analyze_NonEmptyCatchBlock_ReturnsNoFinding()
    {
        var code = """
            try
            {
                DoSomething();
            }
            catch (InvalidOperationException ex)
            {
                Log(ex);
            }
            """;

        var findings = _analyzer.Analyze("Foo.cs", code);

        Assert.DoesNotContain(findings, f => f.Category == ReviewCategory.ExceptionHandling);
    }

    [Fact]
    public void Analyze_TodoComment_ReturnsCodeQualityFinding()
    {
        var findings = _analyzer.Analyze("Foo.cs", "// TODO: fix this\nvar x = 1;");

        Assert.Contains(findings, f => f.Category == ReviewCategory.CodeQuality && f.Severity == Severity.Info);
    }

    [Fact]
    public void Analyze_DebuggerBreak_ReturnsCodeQualityFinding()
    {
        var findings = _analyzer.Analyze("Foo.cs", "Debugger.Break();");

        Assert.Contains(findings, f => f.Category == ReviewCategory.CodeQuality && f.Problem.Contains("Debugger"));
    }

    [Fact]
    public void Analyze_LongMethod_ReturnsMaintainabilityFinding()
    {
        var body = string.Join('\n', Enumerable.Range(0, 60).Select(i => $"        var x{i} = {i};"));
        var code = $$"""
            public class Foo
            {
                public void LongMethod()
                {
            {{body}}
                }
            }
            """;

        var findings = _analyzer.Analyze("Foo.cs", code);

        Assert.Contains(findings, f => f.Category == ReviewCategory.Maintainability);
    }

    [Fact]
    public void Analyze_CleanCode_ReturnsNoFindings()
    {
        var code = """
            public async Task DoWorkAsync()
            {
                var result = await GetTaskAsync();
                try
                {
                    Process(result);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            """;

        var findings = _analyzer.Analyze("Foo.cs", code);

        Assert.Empty(findings);
    }

    [Fact]
    public void Analyze_EmptyString_ReturnsNoFindings()
    {
        var findings = _analyzer.Analyze("Foo.cs", string.Empty);

        Assert.Empty(findings);
    }
}
