namespace AI.CodeReview.Domain;

/// <summary>
/// Named CodeReviewResult (not CodeReview) because "CodeReview" collides with the
/// AI.CodeReview.* root namespace shared by every project in this solution — C# resolves
/// enclosing-namespace members before using-alias directives, so no alias can work around it.
/// </summary>
public sealed record CodeReviewResult(IReadOnlyList<ReviewFinding> Findings);
