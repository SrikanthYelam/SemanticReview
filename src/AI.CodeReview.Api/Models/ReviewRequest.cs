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

    /// <summary>
    /// If true, post the computed findings back to GitUrl as a GitHub pull request review
    /// (inline comments + summary). Requires GitUrl (a pull request URL, not a commit) and
    /// GitHubToken.
    /// </summary>
    public bool PostToGitHub { get; init; }

    /// <summary>
    /// A token with permission to post a review on the target repository (e.g. a GitHub Actions
    /// workflow's own GITHUB_TOKEN, or a personal access token). Required when PostToGitHub is
    /// true; used only for that single publish call, never stored.
    /// </summary>
    public string? GitHubToken { get; init; }

    /// <summary>
    /// Optional project-specific review guidelines (free-form text — typically the contents of a
    /// markdown file the caller's own repo maintains) folded into the AI prompts alongside the
    /// built-in rules. Supplied per request rather than configured server-side, so different
    /// callers/repos can each send their own without this API knowing anything about any specific
    /// repo in advance.
    /// </summary>
    public string? Guidelines { get; init; }
}
