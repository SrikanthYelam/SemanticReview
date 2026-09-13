using AI.CodeReview.Api.Models;
using AI.CodeReview.Application;
using AI.CodeReview.Application.Git;
using AI.CodeReview.Domain;
using Microsoft.AspNetCore.Mvc;

namespace AI.CodeReview.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly ICodeReviewService _codeReviewService;
    private readonly IGitHubReviewPublisher _gitHubReviewPublisher;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(
        ICodeReviewService codeReviewService,
        IGitHubReviewPublisher gitHubReviewPublisher,
        ILogger<ReviewsController> logger)
    {
        _codeReviewService = codeReviewService;
        _gitHubReviewPublisher = gitHubReviewPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Performs an AI-assisted code review of either a unified C# git diff, or a GitHub commit/PR URL.
    /// </summary>
    /// <param name="request">Exactly one of Diff or GitUrl must be provided.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The review completed successfully.</response>
    /// <response code="400">The request was invalid (missing/empty diff and gitUrl, both provided, or an unrecognized gitUrl).</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReviewResponse>> ReviewAsync(
        [FromBody] ReviewRequest request,
        CancellationToken cancellationToken)
    {
        var hasDiff = !string.IsNullOrWhiteSpace(request.Diff);
        var hasGitUrl = !string.IsNullOrWhiteSpace(request.GitUrl);

        if (!hasDiff && !hasGitUrl)
        {
            return Problem(
                detail: "Provide either 'diff' or 'gitUrl'.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        if (hasDiff && hasGitUrl)
        {
            return Problem(
                detail: "Provide only one of 'diff' or 'gitUrl', not both.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        if (request.PostToGitHub && !hasGitUrl)
        {
            return Problem(
                detail: "'postToGitHub' requires 'gitUrl' (a pull request URL); it cannot be used with a raw diff.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        if (request.PostToGitHub && string.IsNullOrWhiteSpace(request.GitHubToken))
        {
            return Problem(
                detail: "'gitHubToken' is required when 'postToGitHub' is true.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }

        try
        {
            var review = hasGitUrl
                ? await _codeReviewService.ReviewFromUrlAsync(request.GitUrl!, cancellationToken)
                : await _codeReviewService.ReviewAsync(request.Diff!, cancellationToken);

            var findingsBySource = review.Findings.ToLookup(f => f.Source);

            GitHubPublishResult? publishResult = null;
            if (request.PostToGitHub)
            {
                try
                {
                    var reviewUrl = await _gitHubReviewPublisher.PublishReviewAsync(
                        request.GitUrl!, request.GitHubToken!, review.Findings, cancellationToken);
                    publishResult = new GitHubPublishResult(Posted: true, ReviewUrl: reviewUrl, Error: null);
                }
                catch (GitHubPublishException ex)
                {
                    // The review itself already succeeded — a failure to post it to GitHub (bad
                    // token, PR not found, GitUrl was a commit not a PR) shouldn't discard the
                    // findings, so this is reported alongside them rather than failing the request.
                    _logger.LogWarning(ex, "Failed to publish review to GitHub");
                    publishResult = new GitHubPublishResult(Posted: false, ReviewUrl: null, Error: ex.Message);
                }
            }

            var response = new ReviewResponse(
                StaticAnalysisFindings: findingsBySource[FindingSource.StaticAnalysis].Select(ToFindingResponse).ToList(),
                AiFindings: findingsBySource[FindingSource.Ai].Select(ToFindingResponse).ToList(),
                GitHubPublish: publishResult);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Rejected invalid review request");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }
        catch (GitDiffFetchException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch diff from git URL");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request");
        }
    }

    private static FindingResponse ToFindingResponse(ReviewFinding finding)
        => new(
            finding.Severity.ToString(),
            finding.Category.ToString(),
            finding.File,
            finding.Line,
            finding.Problem,
            finding.Suggestion);
}
