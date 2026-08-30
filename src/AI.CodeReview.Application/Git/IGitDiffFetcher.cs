namespace AI.CodeReview.Application.Git;

/// <summary>Fetches a unified diff from a git hosting provider given a commit or PR URL.</summary>
public interface IGitDiffFetcher
{
    Task<string> FetchDiffAsync(string gitUrl, CancellationToken cancellationToken = default);
}
