using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AI.CodeReview.Infrastructure.SemanticKernel;

/// <summary>
/// Logs the exact prompt rendered for the model and the raw result received back, at Debug
/// level only. Debug is off by default (appsettings sets "Default": "Information"), so this
/// never surfaces full diff/source content unless a developer explicitly opts in — see README.
/// </summary>
public sealed class AiCallLoggingFilter : IPromptRenderFilter, IFunctionInvocationFilter
{
    private readonly ILogger<AiCallLoggingFilter> _logger;

    public AiCallLoggingFilter(ILogger<AiCallLoggingFilter> logger) => _logger = logger;

    public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
    {
        await next(context);
        _logger.LogDebug(
            "AI request prompt for function {FunctionName}:\n{Prompt}",
            context.Function.Name,
            context.RenderedPrompt);
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        await next(context);
        _logger.LogDebug(
            "AI response for function {FunctionName}:\n{Result}",
            context.Function.Name,
            context.Result);
    }
}
