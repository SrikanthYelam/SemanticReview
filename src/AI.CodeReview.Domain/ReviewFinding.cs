namespace AI.CodeReview.Domain;

public sealed record ReviewFinding(
    Severity Severity,
    ReviewCategory Category,
    string File,
    int? Line,
    string Problem,
    string Suggestion,
    FindingSource Source);
