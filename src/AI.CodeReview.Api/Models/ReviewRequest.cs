namespace AI.CodeReview.Api.Models;

public sealed class ReviewRequest
{
    /// <summary>A unified git diff to review. Provide exactly one of Diff or GitUrl.</summary>
    public string? Diff { get; init; }

    /// <summary>
    /// A GitHub commit or pull request URL to fetch and review, e.g.
    /// https://github.com/{owner}/{repo}/commit/{sha} or https://github.com/{owner}/{repo}/pull/{number}.
    /// Public repositories only. Provide exactly one of Diff or GitUrl.
    /// </summary>
    public string? GitUrl { get; init; }
}
