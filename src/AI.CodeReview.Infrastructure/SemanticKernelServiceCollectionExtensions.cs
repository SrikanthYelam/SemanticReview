using AI.CodeReview.Application.Ai;
using AI.CodeReview.Infrastructure.Options;
using AI.CodeReview.Infrastructure.Plugins;
using AI.CodeReview.Infrastructure.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace AI.CodeReview.Infrastructure;

public static class SemanticKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSemanticKernelServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        // Prefer the OPENAI_API_KEY environment variable over whatever "AI:ApiKey" bound to
        // (appsettings/user-secrets/AI__ApiKey), if it's set. Keeps the real key out of any
        // config file entirely.
        services.PostConfigure<AiOptions>(options =>
        {
            var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envApiKey))
            {
                options.ApiKey = envApiKey;
            }
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            var builder = Kernel.CreateBuilder();

            // Wires SK's own internal logging (prompt rendering, connector diagnostics) into the
            // app's logging pipeline, and registers AiCallLoggingFilter so the exact prompt sent
            // to the model and the raw result received back are available at Debug level.
            // The Kernel builds its own internal IServiceProvider from builder.Services (separate
            // from the app's), so ILogger<T> support has to be registered here explicitly —
            // registering only ILoggerFactory is not sufficient for the generic ILogger<T> to resolve.
            builder.Services.AddSingleton(sp.GetRequiredService<ILoggerFactory>());
            builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            builder.Services.AddSingleton<IPromptRenderFilter, AiCallLoggingFilter>();
            builder.Services.AddSingleton<IFunctionInvocationFilter, AiCallLoggingFilter>();

            switch (options.Provider)
            {
                case "AzureOpenAI":
                    // Documents the "easy to switch provider later" requirement; not exercised
                    // by this MVP, which targets OpenAI.
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: options.DeploymentName
                            ?? throw new InvalidOperationException("AI:DeploymentName is required when AI:Provider is AzureOpenAI."),
                        endpoint: options.Endpoint
                            ?? throw new InvalidOperationException("AI:Endpoint is required when AI:Provider is AzureOpenAI."),
                        apiKey: options.ApiKey);
                    break;

                case "OpenAI":
                default:
                    builder.AddOpenAIChatCompletion(modelId: options.ModelId, apiKey: options.ApiKey);
                    break;
            }

            return builder.Build();
        });

        services.AddSingleton<CodeReviewPlugin>();
        services.AddSingleton<SecurityScanPlugin>();
        services.AddSingleton<IAiCodeReviewer, SemanticKernelCodeReviewer>();

        return services;
    }
}
