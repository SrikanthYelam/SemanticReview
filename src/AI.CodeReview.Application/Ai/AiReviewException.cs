namespace AI.CodeReview.Application.Ai;

/// <summary>
/// Wraps any AI provider failure (auth, rate limiting, timeout, malformed response) into a
/// single exception type so callers never need to know about provider-specific SDK exceptions,
/// and so provider error details/secrets never leak past this boundary.
/// </summary>
public sealed class AiReviewException : Exception
{
    public AiReviewException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
