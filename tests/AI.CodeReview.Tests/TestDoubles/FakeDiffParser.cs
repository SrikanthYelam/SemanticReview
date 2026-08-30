using AI.CodeReview.Application.Diffing;

namespace AI.CodeReview.Tests.TestDoubles;

public sealed class FakeDiffParser : IDiffParser
{
    private readonly IReadOnlyList<ParsedFileDiff> _result;

    public FakeDiffParser(IReadOnlyList<ParsedFileDiff> result) => _result = result;

    public IReadOnlyList<ParsedFileDiff> Parse(string diffText) => _result;
}
