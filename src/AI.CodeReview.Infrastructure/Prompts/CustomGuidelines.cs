namespace AI.CodeReview.Infrastructure.Prompts;

/// <summary>
/// Builds the "additional project-specific guidelines" block folded into both review prompts.
/// Pre-formatted here (rather than left to the prompt template) so an absent/blank guidelines
/// value renders as nothing at all — the basic SK prompt template engine used by both plugins
/// doesn't support conditional sections, only `{{$var}}` substitution.
/// </summary>
internal static class CustomGuidelines
{
    public static string BuildSection(string? guidelines) =>
        string.IsNullOrWhiteSpace(guidelines)
            ? string.Empty
            : $"""

               Additional project-specific guidelines — follow these in addition to the rules above:
               {guidelines}
               """;
}
