namespace AI.CodeReview.Application.Git;

/// <summary>
/// Wraps any failure to resolve a git URL into a diff (unrecognized URL shape, not found,
/// private repo, rate limited) into a single exception type, mirroring AiReviewException.
/// </summary>
public sealed class GitDiffFetchException : Exception
{
    public GitDiffFetchException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
