namespace AI.CodeReview.Api.Models;

public sealed record FindingResponse(
    string Severity,
    string Category,
    string File,
    int? Line,
    string Problem,
    string Suggestion);
