using System.Text.RegularExpressions;

namespace AI.CodeReview.Infrastructure.Git;

/// <summary>Resolved GitHub API path (relative to https://api.github.com/) for a parsed URL.</summary>
public sealed record GitHubDiffRequest(string ApiPath);

/// <summary>
/// Pure, synchronous parser for GitHub commit/pull-request URLs — deliberately has no HTTP
/// dependency so it's unit-testable without mocking. Recognizes:
/// https://github.com/{owner}/{repo}/commit/{sha}
/// https://github.com/{owner}/{repo}/pull/{number}(/anything...)
/// </summary>
public static class GitHubUrlParser
{
    private static readonly Regex CommitPattern = new(
        @"^/(?<owner>[^/]+)/(?<repo>[^/]+)/commit/(?<sha>[0-9a-fA-F]{7,40})/?$",
        RegexOptions.Compiled);

    private static readonly Regex PullPattern = new(
        @"^/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/(?<number>\d+)(?:/.*)?$",
        RegexOptions.Compiled);

    public static bool TryParse(string url, out GitHubDiffRequest? request)
    {
        request = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var commitMatch = CommitPattern.Match(uri.AbsolutePath);
        if (commitMatch.Success)
        {
            request = new GitHubDiffRequest(
                $"repos/{commitMatch.Groups["owner"].Value}/{commitMatch.Groups["repo"].Value}/commits/{commitMatch.Groups["sha"].Value}");
            return true;
        }

        var pullMatch = PullPattern.Match(uri.AbsolutePath);
        if (pullMatch.Success)
        {
            request = new GitHubDiffRequest(
                $"repos/{pullMatch.Groups["owner"].Value}/{pullMatch.Groups["repo"].Value}/pulls/{pullMatch.Groups["number"].Value}");
            return true;
        }

        return false;
    }
}
