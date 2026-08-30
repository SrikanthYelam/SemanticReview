using System.Net;
using System.Text.Json;
using AI.CodeReview.Application.Ai;
using AI.CodeReview.Domain;
using AI.CodeReview.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AI.CodeReview.Infrastructure.SemanticKernel;

public sealed class SemanticKernelCodeReviewer : IAiCodeReviewer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Retries transient failures (timeouts, network errors, 429/5xx) with a short backoff. Not
    // retried: auth/bad-request errors (4xx other than 429) and malformed-JSON responses, since
    // retrying those would just fail again the same way.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)];

    private readonly CodeReviewPlugin _plugin;
    private readonly SecurityScanPlugin _securityScanPlugin;
    private readonly ILogger<SemanticKernelCodeReviewer> _logger;

    public SemanticKernelCodeReviewer(
        CodeReviewPlugin plugin,
        SecurityScanPlugin securityScanPlugin,
        ILogger<SemanticKernelCodeReviewer> logger)
    {
        _plugin = plugin;
        _securityScanPlugin = securityScanPlugin;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        string fileName,
        string diffContext,
        CancellationToken cancellationToken = default)
    {
        var findings = await InvokeAndParseAsync(
            () => _plugin.AnalyzeCodeAsync(fileName, diffContext, cancellationToken),
            fileName,
            "AI code review",
            cancellationToken);

        try
        {
            var securityFindings = await InvokeAndParseAsync(
                () => _securityScanPlugin.ScanAsync(fileName, diffContext, cancellationToken),
                fileName,
                "Security scan",
                cancellationToken);
            findings.AddRange(securityFindings);
        }
        catch (AiReviewException ex)
        {
            // The security scan is a best-effort addition to the general review — a failure here
            // shouldn't discard findings the general review already produced.
            _logger.LogWarning(ex, "Security scan failed for {FileName}; continuing without it", fileName);
        }

        return findings;
    }

    private async Task<List<ReviewFinding>> InvokeAndParseAsync(
        Func<Task<string>> invokePlugin,
        string fileName,
        string operationName,
        CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await InvokeWithRetryAsync(invokePlugin, fileName, operationName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} failed for {FileName}", operationName, fileName);
            throw new AiReviewException($"The {operationName} service failed or is unavailable.", ex);
        }

        AiFindingsResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<AiFindingsResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "{Operation} returned malformed JSON for {FileName}", operationName, fileName);
            throw new AiReviewException($"The {operationName} service returned a malformed response.", ex);
        }

        if (response is null || response.Findings.Count == 0)
        {
            return [];
        }

        var findings = new List<ReviewFinding>(response.Findings.Count);
        foreach (var dto in response.Findings)
        {
            if (!Enum.TryParse<Severity>(dto.Severity, ignoreCase: true, out var severity))
            {
                _logger.LogWarning(
                    "Dropping {Operation} finding with unsupported severity '{Severity}' for {FileName}",
                    operationName, dto.Severity, fileName);
                continue;
            }

            if (!Enum.TryParse<ReviewCategory>(dto.Category, ignoreCase: true, out var category))
            {
                _logger.LogWarning(
                    "Dropping {Operation} finding with unsupported category '{Category}' for {FileName}",
                    operationName, dto.Category, fileName);
                continue;
            }

            findings.Add(new ReviewFinding(
                severity,
                category,
                string.IsNullOrWhiteSpace(dto.File) ? fileName : dto.File,
                dto.Line,
                dto.Problem,
                dto.Suggestion,
                FindingSource.Ai));
        }

        return findings;
    }

    private async Task<string> InvokeWithRetryAsync(
        Func<Task<string>> invokePlugin,
        string fileName,
        string operationName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await invokePlugin();
            }
            catch (Exception ex) when (attempt < RetryDelays.Length && IsTransient(ex))
            {
                _logger.LogWarning(
                    ex, "{Operation} failed for {FileName} (attempt {Attempt}/{MaxAttempts}); retrying",
                    operationName, fileName, attempt + 1, RetryDelays.Length + 1);
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        HttpOperationException { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout } => true,
        HttpOperationException { StatusCode: { } status } => (int)status >= 500,
        HttpOperationException { StatusCode: null } => true, // connection-level failure, no response received
        HttpRequestException => true,
        TimeoutException => true,
        _ => false
    };
}
