using AI.CodeReview.Application.Git;
using Microsoft.Extensions.DependencyInjection;

namespace AI.CodeReview.Infrastructure.Git;

public static class GitServiceCollectionExtensions
{
    public static IServiceCollection AddGitDiffFetching(this IServiceCollection services)
    {
        services.AddHttpClient<IGitDiffFetcher, GitHubDiffFetcher>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            // GitHub's API rejects requests without a User-Agent header.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AI.CodeReview.Api");
        });

        return services;
    }

    public static IServiceCollection AddGitHubReviewPublishing(this IServiceCollection services)
    {
        services.AddHttpClient<IGitHubReviewPublisher, GitHubReviewPublisher>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AI.CodeReview.Api");
        });

        return services;
    }
}
