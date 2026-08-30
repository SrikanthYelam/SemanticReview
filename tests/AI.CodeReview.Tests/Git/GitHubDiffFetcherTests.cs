using System.Net;
using AI.CodeReview.Application.Git;
using AI.CodeReview.Infrastructure.Git;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.CodeReview.Tests.Git;

public class GitHubDiffFetcherTests
{
    [Fact]
    public async Task FetchDiffAsync_SuccessfulResponse_ReturnsBodyVerbatimAndUsesResolvedApiPath()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("diff --git a/Foo.cs b/Foo.cs\n+var x = 1;\n")
        });
        var fetcher = CreateFetcher(handler);

        var diff = await fetcher.FetchDiffAsync("https://github.com/owner/repo/commit/abc1234");

        Assert.Equal("diff --git a/Foo.cs b/Foo.cs\n+var x = 1;\n", diff);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/owner/repo/commits/abc1234", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task FetchDiffAsync_NotFound_ThrowsGitDiffFetchException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var fetcher = CreateFetcher(handler);

        await Assert.ThrowsAsync<GitDiffFetchException>(
            () => fetcher.FetchDiffAsync("https://github.com/owner/repo/commit/abc1234"));
    }

    [Fact]
    public async Task FetchDiffAsync_Forbidden_ThrowsGitDiffFetchException()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var fetcher = CreateFetcher(handler);

        await Assert.ThrowsAsync<GitDiffFetchException>(
            () => fetcher.FetchDiffAsync("https://github.com/owner/repo/commit/abc1234"));
    }

    [Fact]
    public async Task FetchDiffAsync_UnrecognizedUrl_ThrowsWithoutMakingHttpCall()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var fetcher = CreateFetcher(handler);

        await Assert.ThrowsAsync<GitDiffFetchException>(
            () => fetcher.FetchDiffAsync("https://gitlab.com/owner/repo/commit/abc1234"));

        Assert.Empty(handler.Requests);
    }

    private static GitHubDiffFetcher CreateFetcher(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubDiffFetcher(httpClient, NullLogger<GitHubDiffFetcher>.Instance);
    }
}
