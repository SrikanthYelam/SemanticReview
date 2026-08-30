using System.ComponentModel;
using AI.CodeReview.Infrastructure.Prompts;
using AI.CodeReview.Infrastructure.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AI.CodeReview.Infrastructure.Plugins;

/// <summary>
/// Single-responsibility Semantic Kernel plugin: reviews a C# diff fragment and returns
/// structured JSON findings. Not used for agentic auto function-calling — the review pipeline
/// invokes this function directly, one file at a time.
/// </summary>
public sealed class CodeReviewPlugin
{
    private readonly Kernel _kernel;
    private readonly KernelFunction _reviewFunction;

    public CodeReviewPlugin(Kernel kernel)
    {
        _kernel = kernel;

        // AllowDangerouslySetContent: SK's prompt template engine HTML-encodes injected variables
        // by default (e.g. "<" becomes "&lt;"), which corrupts real C# source code (generics,
        // comparisons, lambdas) before the model ever sees it. The diff content isn't rendered as
        // HTML anywhere downstream, so that protection has no benefit here and only breaks input.
        // The flag must be set on each InputVariable explicitly — PromptTemplateConfig.InputVariables
        // isn't auto-populated from the {{$var}} references in the template text, and the top-level
        // PromptTemplateConfig.AllowDangerouslySetContent alone does not disable per-variable encoding.
        var promptConfig = new PromptTemplateConfig(CodeReviewPrompt.Template)
        {
            Name = "AnalyzeCode",
            Description = "Analyzes C# code changes for correctness, security, performance, and maintainability issues.",
            AllowDangerouslySetContent = true
        };
        promptConfig.InputVariables.Add(new InputVariable { Name = "fileName", AllowDangerouslySetContent = true });
        promptConfig.InputVariables.Add(new InputVariable { Name = "diffContext", AllowDangerouslySetContent = true });

        _reviewFunction = KernelFunctionFactory.CreateFromPrompt(promptConfig);
    }

    [KernelFunction("AnalyzeCode")]
    [Description("Analyze C# code changes for correctness, security, performance, and maintainability issues.")]
    public async Task<string> AnalyzeCodeAsync(
        string fileName,
        string diffContext,
        CancellationToken cancellationToken = default)
    {
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(AiFindingsResponse),
            Temperature = 0
        };

        var arguments = new KernelArguments(executionSettings)
        {
            ["fileName"] = fileName,
            ["diffContext"] = diffContext
        };

        var result = await _kernel.InvokeAsync(_reviewFunction, arguments, cancellationToken);
        return result.GetValue<string>() ?? """{"findings":[]}""";
    }
}
