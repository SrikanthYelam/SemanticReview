namespace AI.CodeReview.Api.Models;

public sealed record ReviewResponse(
    IReadOnlyList<FindingResponse> StaticAnalysisFindings,
    IReadOnlyList<FindingResponse> AiFindings);
