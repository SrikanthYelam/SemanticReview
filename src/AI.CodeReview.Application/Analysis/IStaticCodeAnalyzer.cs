using AI.CodeReview.Domain;

namespace AI.CodeReview.Application.Analysis;

/// <summary>
/// Deterministic (non-AI) static analysis of a C# code fragment. Line numbers in returned
/// findings are local to the supplied <paramref name="code"/> text (1-based); callers are
/// responsible for reconciling them against the original file/diff if needed.
/// </summary>
public interface IStaticCodeAnalyzer
{
    IReadOnlyList<ReviewFinding> Analyze(string fileName, string code);
}
