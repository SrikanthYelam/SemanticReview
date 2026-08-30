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
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(ICodeReviewService codeReviewService, ILogger<ReviewsController> logger)
    {
        _codeReviewService = codeReviewService;
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

        try
        {
            var review = hasGitUrl
                ? await _codeReviewService.ReviewFromUrlAsync(request.GitUrl!, cancellationToken)
                : await _codeReviewService.ReviewAsync(request.Diff!, cancellationToken);

            var findingsBySource = review.Findings.ToLookup(f => f.Source);

            var response = new ReviewResponse(
                StaticAnalysisFindings: findingsBySource[FindingSource.StaticAnalysis].Select(ToFindingResponse).ToList(),
                AiFindings: findingsBySource[FindingSource.Ai].Select(ToFindingResponse).ToList());

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
