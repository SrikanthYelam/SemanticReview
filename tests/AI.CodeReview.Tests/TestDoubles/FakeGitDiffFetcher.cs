using AI.CodeReview.Application.Git;

namespace AI.CodeReview.Tests.TestDoubles;

public sealed class FakeGitDiffFetcher : IGitDiffFetcher
{
    private readonly string _diffToReturn;
    private readonly Exception? _exceptionToThrow;

    public List<string> RequestedUrls { get; } = [];

    public FakeGitDiffFetcher(string diffToReturn = "", Exception? exceptionToThrow = null)
    {
        _diffToReturn = diffToReturn;
        _exceptionToThrow = exceptionToThrow;
    }

    public Task<string> FetchDiffAsync(string gitUrl, CancellationToken cancellationToken = default)
    {
        RequestedUrls.Add(gitUrl);

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(_diffToReturn);
    }
}
