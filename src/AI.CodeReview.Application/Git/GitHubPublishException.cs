namespace AI.CodeReview.Application.Git;

/// <summary>
/// Wraps any failure to publish a review back to GitHub (bad/insufficient token, PR not found,
/// rate limited, malformed payload) into a single exception type, mirroring GitDiffFetchException.
/// Unlike GitDiffFetchException, the review has already been computed by the time this is thrown,
/// so callers may choose to report it alongside that review instead of failing the whole request
/// — see ReviewsController.
/// </summary>
public sealed class GitHubPublishException : Exception
{
    public GitHubPublishException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
