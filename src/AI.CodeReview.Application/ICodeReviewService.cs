using AI.CodeReview.Domain;

namespace AI.CodeReview.Application;

public interface ICodeReviewService
{
    /// <param name="guidelines">
    /// Optional project-specific review guidelines (free-form text, typically the contents of a
    /// markdown file the caller maintains) folded into the AI prompts alongside the built-in
    /// rules. Supplied per request, not server-side config — different callers/repos can each send
    /// their own.
    /// </param>
    Task<CodeReviewResult> ReviewAsync(string diff, string? guidelines = null, CancellationToken cancellationToken = default);

    /// <summary>Fetches the diff for a GitHub commit or pull request URL, then reviews it.</summary>
    /// <param name="guidelines">See <see cref="ReviewAsync"/>.</param>
    Task<CodeReviewResult> ReviewFromUrlAsync(string gitUrl, string? guidelines = null, CancellationToken cancellationToken = default);
}
