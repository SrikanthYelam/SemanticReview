namespace AI.CodeReview.Infrastructure.Options;

/// <summary>Strongly typed binding of the "AI" configuration section. Never hard-code these values.</summary>
public sealed class AiOptions
{
    public const string SectionName = "AI";

    /// <summary>"OpenAI" or "AzureOpenAI".</summary>
    public string Provider { get; set; } = "OpenAI";

    /// <summary>Required for AzureOpenAI; ignored for OpenAI.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Azure OpenAI deployment name. Required for AzureOpenAI; ignored for OpenAI.</summary>
    public string? DeploymentName { get; set; }

    /// <summary>OpenAI model id, e.g. "gpt-4o-mini". Required for OpenAI.</summary>
    public string ModelId { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Falls back to the OPENAI_API_KEY environment variable if not bound from the "AI:ApiKey"
    /// config key — see AddSemanticKernelServices' PostConfigure step.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
