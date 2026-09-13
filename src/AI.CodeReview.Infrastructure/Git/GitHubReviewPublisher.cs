using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.CodeReview.Application.Git;
using AI.CodeReview.Domain;
using Microsoft.Extensions.Logging;

namespace AI.CodeReview.Infrastructure.Git;

/// <summary>
/// Publishes review findings back to a GitHub pull request as a single review: findings with a
/// line number become inline comments, findings without one are listed in the review's summary
/// body instead of being dropped. The caller supplies the access token per call (e.g. a GitHub
/// Actions workflow's own GITHUB_TOKEN) — this class never holds a credential itself.
/// </summary>
public sealed class GitHubReviewPublisher : IGitHubReviewPublisher
{
    // GitHub's API expects lowercase field names (body/event/comments/path/line); camelCase
    // happens to match exactly since every field here is a single word.
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubReviewPublisher> _logger;

    public GitHubReviewPublisher(HttpClient httpClient, ILogger<GitHubReviewPublisher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> PublishReviewAsync(
        string pullRequestUrl,
        string accessToken,
        IReadOnlyList<ReviewFinding> findings,
        CancellationToken cancellationToken = default)
    {
        if (!GitHubUrlParser.TryParsePullRequest(pullRequestUrl, out var pr) || pr is null)
        {
            throw new GitHubPublishException(
                "The URL is not a recognized GitHub pull request URL (expected https://github.com/{owner}/{repo}/pull/{number}); only pull requests can receive a posted review.");
        }

        var located = findings.Where(f => f.Line is not null).ToList();
        var unlocated = findings.Where(f => f.Line is null).ToList();

        var payload = new GitHubReviewRequest(
            Body: BuildSummary(findings, unlocated),
            Event: "COMMENT",
            Comments: located
                .Select(f => new GitHubReviewComment(f.File, f.Line!.Value, BuildCommentBody(f)))
                .ToList());

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"repos/{pr.Owner}/{pr.Repo}/pulls/{pr.Number}/reviews")
        {
            Content = JsonContent.Create(payload, options: RequestJsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitHub API request failed while publishing review for {Owner}/{Repo}#{Number}", pr.Owner, pr.Repo, pr.Number);
            throw new GitHubPublishException("Could not reach the GitHub API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub API returned {StatusCode} while publishing review for {Owner}/{Repo}#{Number}", response.StatusCode, pr.Owner, pr.Repo, pr.Number);
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new GitHubPublishException("GitHub rejected the provided token (401 Unauthorized)."),
                HttpStatusCode.Forbidden => new GitHubPublishException("The provided token does not have permission to post a review on this repository (403 Forbidden)."),
                HttpStatusCode.NotFound => new GitHubPublishException("The pull request was not found (it may not exist, or the token lacks access)."),
                HttpStatusCode.UnprocessableEntity => new GitHubPublishException("GitHub rejected the review payload (422) — a finding's file/line may not be part of this pull request's diff."),
                _ => new GitHubPublishException($"GitHub API request failed with status {(int)response.StatusCode}.")
            };
        }

        var result = await response.Content.ReadFromJsonAsync<GitHubReviewApiResponse>(cancellationToken: cancellationToken);
        return result?.HtmlUrl ?? $"https://github.com/{pr.Owner}/{pr.Repo}/pull/{pr.Number}";
    }

    private static string BuildCommentBody(ReviewFinding finding)
        => $"**[{finding.Severity}] {finding.Category}**\n\n{finding.Problem}\n\n**Suggestion:** {finding.Suggestion}";

    private static string BuildSummary(IReadOnlyList<ReviewFinding> findings, IReadOnlyList<ReviewFinding> unlocated)
    {
        if (findings.Count == 0)
        {
            return "AI Code Review Assistant found no issues in this diff.";
        }

        var summary = $"AI Code Review Assistant found {findings.Count} finding(s) — see inline comments.";
        if (unlocated.Count == 0)
        {
            return summary;
        }

        var extra = unlocated.Select(f => $"- **[{f.Severity}] {f.File}** ({f.Category}): {f.Problem}");
        return summary + "\n\nFindings without a specific line number:\n" + string.Join('\n', extra);
    }

    private sealed record GitHubReviewRequest(
        string Body,
        string Event,
        IReadOnlyList<GitHubReviewComment> Comments);

    private sealed record GitHubReviewComment(string Path, int Line, string Body);

    private sealed record GitHubReviewApiResponse([property: JsonPropertyName("html_url")] string? HtmlUrl);
}
