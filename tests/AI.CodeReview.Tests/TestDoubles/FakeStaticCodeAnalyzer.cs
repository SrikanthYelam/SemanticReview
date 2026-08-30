using AI.CodeReview.Application.Analysis;
using AI.CodeReview.Domain;

namespace AI.CodeReview.Tests.TestDoubles;

public sealed class FakeStaticCodeAnalyzer : IStaticCodeAnalyzer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ReviewFinding>> _findingsByFile;

    public List<string> AnalyzedFileNames { get; } = [];

    public FakeStaticCodeAnalyzer(IReadOnlyDictionary<string, IReadOnlyList<ReviewFinding>>? findingsByFile = null)
        => _findingsByFile = findingsByFile ?? new Dictionary<string, IReadOnlyList<ReviewFinding>>();

    public IReadOnlyList<ReviewFinding> Analyze(string fileName, string code)
    {
        AnalyzedFileNames.Add(fileName);
        return _findingsByFile.TryGetValue(fileName, out var findings) ? findings : Array.Empty<ReviewFinding>();
    }
}
