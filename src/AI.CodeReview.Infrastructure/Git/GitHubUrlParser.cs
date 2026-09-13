using System.Text.RegularExpressions;

namespace AI.CodeReview.Infrastructure.Git;

/// <summary>Resolved GitHub API path (relative to https://api.github.com/) for a parsed URL.</summary>
public sealed record GitHubDiffRequest(string ApiPath);

/// <summary>A parsed GitHub pull request reference, for endpoints that need owner/repo/number separately (e.g. posting a review) rather than a single pre-built path.</summary>
public sealed record GitHubPullRequestRef(string Owner, string Repo, int Number);

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

        if (!TryGetGitHubPath(url, out var path))
        {
            return false;
        }

        var commitMatch = CommitPattern.Match(path);
        if (commitMatch.Success)
        {
            request = new GitHubDiffRequest(
                $"repos/{commitMatch.Groups["owner"].Value}/{commitMatch.Groups["repo"].Value}/commits/{commitMatch.Groups["sha"].Value}");
            return true;
        }

        var pullMatch = PullPattern.Match(path);
        if (pullMatch.Success)
        {
            request = new GitHubDiffRequest(
                $"repos/{pullMatch.Groups["owner"].Value}/{pullMatch.Groups["repo"].Value}/pulls/{pullMatch.Groups["number"].Value}");
            return true;
        }

        return false;
    }

    /// <summary>Parses a pull request URL specifically, exposing owner/repo/number rather than a pre-built path — needed to build the "post review" API path, which nests under the PR number differently than the diff-fetch path.</summary>
    public static bool TryParsePullRequest(string url, out GitHubPullRequestRef? pullRequest)
    {
        pullRequest = null;

        if (!TryGetGitHubPath(url, out var path))
        {
            return false;
        }

        var pullMatch = PullPattern.Match(path);
        if (!pullMatch.Success)
        {
            return false;
        }

        pullRequest = new GitHubPullRequestRef(
            pullMatch.Groups["owner"].Value,
            pullMatch.Groups["repo"].Value,
            int.Parse(pullMatch.Groups["number"].Value));
        return true;
    }

    private static bool TryGetGitHubPath(string url, out string path)
    {
        path = string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        path = uri.AbsolutePath;
        return true;
    }
}
