using AI.CodeReview.Domain;

namespace AI.CodeReview.Application.Ai;

/// <summary>AI-assisted (LLM-backed) code review for a single file's changes.</summary>
public interface IAiCodeReviewer
{
    Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        string fileName,
        string diffContext,
        CancellationToken cancellationToken = default);
}
