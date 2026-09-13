namespace AI.CodeReview.Api.Models;

public sealed record ReviewResponse(
    IReadOnlyList<FindingResponse> StaticAnalysisFindings,
    IReadOnlyList<FindingResponse> AiFindings,
    GitHubPublishResult? GitHubPublish = null);

/// <summary>Result of an opt-in PostToGitHub request. Present only when PostToGitHub was set on the request.</summary>
public sealed record GitHubPublishResult(bool Posted, string? ReviewUrl, string? Error);
