using AI.CodeReview.Domain;

namespace AI.CodeReview.Application.Ai;

/// <summary>AI-assisted (LLM-backed) code review for a single file's changes.</summary>
public interface IAiCodeReviewer
{
    /// <param name="guidelines">Optional project-specific review guidelines folded into the prompt alongside the built-in rules.</param>
    Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        string fileName,
        string diffContext,
        string? guidelines = null,
        CancellationToken cancellationToken = default);
}
