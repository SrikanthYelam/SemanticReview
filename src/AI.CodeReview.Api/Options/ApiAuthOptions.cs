namespace AI.CodeReview.Api.Options;

/// <summary>Strongly typed binding of the "Api" configuration section.</summary>
public sealed class ApiAuthOptions
{
    public const string SectionName = "Api";

    /// <summary>
    /// If set, every /api request must include a matching X-Api-Key header. Left empty (the
    /// default), the API is open — matches the previous, unauthenticated behavior.
    /// </summary>
    public string? ApiKey { get; set; }
}
