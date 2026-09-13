using AI.CodeReview.Domain;

namespace AI.CodeReview.Application.Git;

/// <summary>Publishes computed review findings back to a git hosting provider as a pull request review.</summary>
public interface IGitHubReviewPublisher
{
    /// <returns>The URL of the posted review.</returns>
    Task<string> PublishReviewAsync(
        string pullRequestUrl,
        string accessToken,
        IReadOnlyList<ReviewFinding> findings,
        CancellationToken cancellationToken = default);
}
