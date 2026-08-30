using System.Text.Json.Serialization;

namespace AI.CodeReview.Infrastructure.SemanticKernel;

/// <summary>
/// Shape of the AI's structured JSON response. Severity/Category are strings (not the domain
/// enums) because model output is untrusted until validated and mapped by the caller.
/// </summary>
public sealed class AiFindingsResponse
{
    [JsonPropertyName("findings")]
    public List<AiFindingDto> Findings { get; init; } = [];
}

public sealed class AiFindingDto
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("file")]
    public string? File { get; init; }

    [JsonPropertyName("line")]
    public int? Line { get; init; }

    [JsonPropertyName("problem")]
    public string Problem { get; init; } = string.Empty;

    [JsonPropertyName("suggestion")]
    public string Suggestion { get; init; } = string.Empty;
}
