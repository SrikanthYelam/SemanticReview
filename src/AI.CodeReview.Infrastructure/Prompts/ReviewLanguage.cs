namespace AI.CodeReview.Infrastructure.Prompts;

/// <summary>
/// Maps a file name to the language name injected into the review prompts, so a TypeScript or
/// Python file isn't reviewed by a "C# code reviewer". Covers the same extensions the orchestrator
/// routes to the AI reviewer (deliberately not YAML/JSON — see
/// <c>CodeReviewOrchestrator.AiOnlyReviewExtensions</c> for why).
/// </summary>
public static class ReviewLanguage
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "C#",
        [".ts"] = "TypeScript", [".tsx"] = "TypeScript",
        [".js"] = "JavaScript", [".jsx"] = "JavaScript",
        [".py"] = "Python",
        [".java"] = "Java",
        [".go"] = "Go",
        [".rb"] = "Ruby",
        [".php"] = "PHP",
        [".razor"] = "Razor", [".cshtml"] = "Razor",
        [".sql"] = "SQL",
        [".html"] = "HTML",
        [".css"] = "CSS",
        [".scss"] = "SCSS"
    };

    public static string FromFileName(string fileName)
        => ByExtension.GetValueOrDefault(Path.GetExtension(fileName)) ?? "software";
}
