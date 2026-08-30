using AI.CodeReview.Domain;

namespace AI.CodeReview.Application;

public interface ICodeReviewService
{
    Task<CodeReviewResult> ReviewAsync(string diff, CancellationToken cancellationToken = default);

    /// <summary>Fetches the diff for a GitHub commit or pull request URL, then reviews it.</summary>
    Task<CodeReviewResult> ReviewFromUrlAsync(string gitUrl, CancellationToken cancellationToken = default);
}
