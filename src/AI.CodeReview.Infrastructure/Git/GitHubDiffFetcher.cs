using System.Net;
using System.Net.Http.Headers;
using AI.CodeReview.Application.Git;
using Microsoft.Extensions.Logging;

namespace AI.CodeReview.Infrastructure.Git;

/// <summary>
/// Fetches a diff from GitHub's REST API for a commit or pull-request URL. Public repositories
/// only — no auth token is configured, so this relies on GitHub's unauthenticated rate limit
/// (60 requests/hour per IP) and cannot see private repos.
/// </summary>
public sealed class GitHubDiffFetcher : IGitDiffFetcher
{
    private static readonly MediaTypeWithQualityHeaderValue DiffMediaType = new("application/vnd.github.v3.diff");

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubDiffFetcher> _logger;

    public GitHubDiffFetcher(HttpClient httpClient, ILogger<GitHubDiffFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> FetchDiffAsync(string gitUrl, CancellationToken cancellationToken = default)
    {
        if (!GitHubUrlParser.TryParse(gitUrl, out var request) || request is null)
        {
            throw new GitDiffFetchException(
                "The URL is not a recognized GitHub commit or pull request URL (expected https://github.com/{owner}/{repo}/commit/{sha} or https://github.com/{owner}/{repo}/pull/{number}).");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, request.ApiPath);
        httpRequest.Headers.Accept.Add(DiffMediaType);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitHub API request failed for {ApiPath}", request.ApiPath);
            throw new GitDiffFetchException("Could not reach the GitHub API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub API returned {StatusCode} for {ApiPath}", response.StatusCode, request.ApiPath);
            throw response.StatusCode switch
            {
                HttpStatusCode.NotFound => new GitDiffFetchException(
                    "The commit or pull request was not found (it may not exist, or the repository may be private)."),
                HttpStatusCode.Forbidden => new GitDiffFetchException(
                    "GitHub API rate limit reached (60 requests/hour, unauthenticated). Try again later."),
                _ => new GitDiffFetchException($"GitHub API request failed with status {(int)response.StatusCode}.")
            };
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
