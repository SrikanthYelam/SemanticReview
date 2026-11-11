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
    /// <param name="minimumSeverity">
    /// Findings less severe than this are dropped before dedupe/sort. Defaults to
    /// <see cref="Severity.Low"/> (excludes only <see cref="Severity.Info"/>) — supplied per
    /// request rather than server-side config, same reasoning as <paramref name="guidelines"/>.
    /// </param>
    Task<CodeReviewResult> ReviewAsync(
        string diff,
        string? guidelines = null,
        Severity minimumSeverity = Severity.Low,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches the diff for a GitHub commit or pull request URL, then reviews it.</summary>
    /// <param name="guidelines">See <see cref="ReviewAsync"/>.</param>
    /// <param name="minimumSeverity">See <see cref="ReviewAsync"/>.</param>
    Task<CodeReviewResult> ReviewFromUrlAsync(
        string gitUrl,
        string? guidelines = null,
        Severity minimumSeverity = Severity.Low,
        CancellationToken cancellationToken = default);
}
