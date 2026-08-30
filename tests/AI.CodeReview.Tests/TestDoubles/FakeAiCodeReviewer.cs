using AI.CodeReview.Application.Ai;
using AI.CodeReview.Domain;

namespace AI.CodeReview.Tests.TestDoubles;

public sealed class FakeAiCodeReviewer : IAiCodeReviewer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ReviewFinding>> _findingsByFile;
    private readonly Exception? _exceptionToThrow;

    public List<string> ReviewedFileNames { get; } = [];

    public FakeAiCodeReviewer(
        IReadOnlyDictionary<string, IReadOnlyList<ReviewFinding>>? findingsByFile = null,
        Exception? exceptionToThrow = null)
    {
        _findingsByFile = findingsByFile ?? new Dictionary<string, IReadOnlyList<ReviewFinding>>();
        _exceptionToThrow = exceptionToThrow;
    }

    public Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        string fileName,
        string diffContext,
        CancellationToken cancellationToken = default)
    {
        ReviewedFileNames.Add(fileName);

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        var findings = _findingsByFile.TryGetValue(fileName, out var value) ? value : Array.Empty<ReviewFinding>();
        return Task.FromResult(findings);
    }
}
