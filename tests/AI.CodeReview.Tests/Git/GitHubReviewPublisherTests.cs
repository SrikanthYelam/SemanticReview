using System.Net;
using System.Text.Json;
using AI.CodeReview.Application.Git;
using AI.CodeReview.Domain;
using AI.CodeReview.Infrastructure.Git;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AI.CodeReview.Tests.Git;

public class GitHubReviewPublisherTests
{
    private const string PullRequestUrl = "https://github.com/owner/repo/pull/42";

    [Fact]
    public async Task PublishReviewAsync_SuccessfulResponse_PostsInlineCommentsAndReturnsReviewUrl()
    {
        var findings = new List<ReviewFinding>
        {
            new(Severity.High, ReviewCategory.Async, "src/Foo.cs", 17, ".Result usage", "Await instead.", FindingSource.StaticAnalysis)
        };

        // The request (and its Content) is disposed by PublishReviewAsync's own `using` before
        // this method returns, so the body has to be captured inside the responder, not after.
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            using var reader = new StreamReader(request.Content!.ReadAsStream());
            capturedBody = reader.ReadToEnd();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"html_url":"https://github.com/owner/repo/pull/42#pullrequestreview-1"}""")
            };
        });
        var publisher = CreatePublisher(handler);

        var reviewUrl = await publisher.PublishReviewAsync(PullRequestUrl, "token-123", findings);

        Assert.Equal("https://github.com/owner/repo/pull/42#pullrequestreview-1", reviewUrl);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/owner/repo/pulls/42/reviews", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token-123", request.Headers.Authorization!.Parameter);

        using var body = JsonDocument.Parse(capturedBody!);
        Assert.Equal("COMMENT", body.RootElement.GetProperty("event").GetString());
        var comment = Assert.Single(body.RootElement.GetProperty("comments").EnumerateArray());
        Assert.Equal("src/Foo.cs", comment.GetProperty("path").GetString());
        Assert.Equal(17, comment.GetProperty("line").GetInt32());
        Assert.Contains(".Result usage", comment.GetProperty("body").GetString());
    }

    [Fact]
    public async Task PublishReviewAsync_FindingWithoutLine_IsListedInSummaryNotDropped()
    {
        var findings = new List<ReviewFinding>
        {
            new(Severity.Low, ReviewCategory.Maintainability, "src/Foo.cs", null, "Method too long", "Split it up.", FindingSource.StaticAnalysis)
        };

        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            using var reader = new StreamReader(request.Content!.ReadAsStream());
            capturedBody = reader.ReadToEnd();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"html_url":"https://github.com/owner/repo/pull/42#pullrequestreview-1"}""")
            };
        });
        var publisher = CreatePublisher(handler);

        await publisher.PublishReviewAsync(PullRequestUrl, "token-123", findings);

        using var body = JsonDocument.Parse(capturedBody!);
        Assert.Empty(body.RootElement.GetProperty("comments").EnumerateArray());
        Assert.Contains("Method too long", body.RootElement.GetProperty("body").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task PublishReviewAsync_ErrorStatusCodes_ThrowGitHubPublishException(HttpStatusCode statusCode)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
        var publisher = CreatePublisher(handler);

        await Assert.ThrowsAsync<GitHubPublishException>(
            () => publisher.PublishReviewAsync(PullRequestUrl, "token-123", []));
    }

    [Fact]
    public async Task PublishReviewAsync_NotAPullRequestUrl_ThrowsWithoutMakingHttpCall()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var publisher = CreatePublisher(handler);

        await Assert.ThrowsAsync<GitHubPublishException>(
            () => publisher.PublishReviewAsync("https://github.com/owner/repo/commit/abc1234", "token-123", []));

        Assert.Empty(handler.Requests);
    }

    private static GitHubReviewPublisher CreatePublisher(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubReviewPublisher(httpClient, NullLogger<GitHubReviewPublisher>.Instance);
    }
}
