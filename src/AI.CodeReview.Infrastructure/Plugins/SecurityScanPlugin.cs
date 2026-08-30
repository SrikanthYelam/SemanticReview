using System.ComponentModel;
using AI.CodeReview.Infrastructure.Prompts;
using AI.CodeReview.Infrastructure.SemanticKernel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AI.CodeReview.Infrastructure.Plugins;

/// <summary>
/// Single-responsibility Semantic Kernel plugin: scans a C# diff fragment for security
/// vulnerabilities (secrets, injection, unsafe deserialization, weak crypto, etc.) using a
/// narrower prompt than the general <see cref="CodeReviewPlugin"/>. Reuses the same structured
/// output shape (<see cref="AiFindingsResponse"/>) as the general reviewer.
/// </summary>
public sealed class SecurityScanPlugin
{
    private readonly Kernel _kernel;
    private readonly KernelFunction _scanFunction;

    public SecurityScanPlugin(Kernel kernel)
    {
        _kernel = kernel;

        // See CodeReviewPlugin for why AllowDangerouslySetContent is required on every variable.
        var promptConfig = new PromptTemplateConfig(SecurityScanPrompt.Template)
        {
            Name = "ScanForSecurityIssues",
            Description = "Scans C# code changes for security vulnerabilities.",
            AllowDangerouslySetContent = true
        };
        promptConfig.InputVariables.Add(new InputVariable { Name = "fileName", AllowDangerouslySetContent = true });
        promptConfig.InputVariables.Add(new InputVariable { Name = "diffContext", AllowDangerouslySetContent = true });

        _scanFunction = KernelFunctionFactory.CreateFromPrompt(promptConfig);
    }

    [KernelFunction("ScanForSecurityIssues")]
    [Description("Scan C# code changes for security vulnerabilities such as hardcoded secrets, injection, and weak cryptography.")]
    public async Task<string> ScanAsync(
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

        var result = await _kernel.InvokeAsync(_scanFunction, arguments, cancellationToken);
        return result.GetValue<string>() ?? """{"findings":[]}""";
    }
}
