using AI.CodeReview.Infrastructure.Git;
using Xunit;

namespace AI.CodeReview.Tests.Git;

public class GitHubUrlParserTests
{
    [Fact]
    public void TryParse_CommitUrl_ResolvesToCommitsApiPath()
    {
        var success = GitHubUrlParser.TryParse("https://github.com/dotnet/runtime/commit/abc1234", out var request);

        Assert.True(success);
        Assert.Equal("repos/dotnet/runtime/commits/abc1234", request!.ApiPath);
    }

    [Fact]
    public void TryParse_PullRequestUrl_ResolvesToPullsApiPath()
    {
        var success = GitHubUrlParser.TryParse("https://github.com/dotnet/runtime/pull/12345", out var request);

        Assert.True(success);
        Assert.Equal("repos/dotnet/runtime/pulls/12345", request!.ApiPath);
    }

    [Fact]
    public void TryParse_PullRequestUrlWithTrailingSegment_StillResolves()
    {
        var success = GitHubUrlParser.TryParse("https://github.com/dotnet/runtime/pull/12345/files", out var request);

        Assert.True(success);
        Assert.Equal("repos/dotnet/runtime/pulls/12345", request!.ApiPath);
    }

    [Theory]
    [InlineData("https://gitlab.com/owner/repo/commit/abc1234")]
    [InlineData("https://bitbucket.org/owner/repo/commits/abc1234")]
    [InlineData("not a url")]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo/issues/1")]
    [InlineData("ftp://github.com/owner/repo/commit/abc1234")]
    public void TryParse_UnrecognizedUrl_ReturnsFalse(string url)
    {
        var success = GitHubUrlParser.TryParse(url, out var request);

        Assert.False(success);
        Assert.Null(request);
    }
}
