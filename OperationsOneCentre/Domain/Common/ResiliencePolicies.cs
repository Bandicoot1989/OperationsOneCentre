using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;

namespace OperationsOneCentre.Domain.Common;

/// <summary>
/// Centralized resilience policies for external API calls.
/// Provides retry with exponential backoff for transient failures.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Standard retry pipeline for HTTP requests to external APIs (Jira, Confluence, Azure).
    /// Retries on 429 (rate limited), 502, 503, 504 with exponential backoff.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpRetryPipeline(ILogger? logger = null)
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.BadGateway)
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
                    .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.RequestTimeout),
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        "HTTP retry attempt {Attempt} after {StatusCode}. Delay: {Delay}ms",
                        args.AttemptNumber,
                        args.Outcome.Result?.StatusCode,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Standard retry pipeline for Azure OpenAI API calls.
    /// Handles RateLimited responses and transient failures.
    /// </summary>
    public static ResiliencePipeline CreateOpenAiRetryPipeline(ILogger? logger = null)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<System.ClientModel.ClientResultException>(ex =>
                        ex.Message.Contains("429") || ex.Message.Contains("503")),
                OnRetry = args =>
                {
                    logger?.LogWarning(
                        "OpenAI retry attempt {Attempt}. Exception: {Message}. Delay: {Delay}ms",
                        args.AttemptNumber,
                        args.Outcome.Exception?.Message ?? "unknown",
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
