using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Extension methods for configuring HTTP client resilience with Polly.
/// </summary>
public static class HttpClientResilienceExtensions
{
    /// <summary>
    /// Adds resilience (retry, circuit breaker, timeout) to an HTTP client.
    /// </summary>
    public static IHttpClientBuilder AddStandardResilienceHandler(
        this IHttpClientBuilder builder,
        int maxRetries = 3,
        int circuitBreakerThreshold = 5,
        int timeoutSeconds = 30)
    {
        builder.AddResilienceHandler("standard", (resilienceBuilder, context) =>
        {
            // Retry with exponential backoff
            resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(500),
                ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome))
            });

            // Circuit breaker
            resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.5,
                MinimumThroughput = circuitBreakerThreshold,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = args => ValueTask.FromResult(ShouldBreak(args.Outcome))
            });

            // Timeout
            resilienceBuilder.AddTimeout(TimeSpan.FromSeconds(timeoutSeconds));
        });

        return builder;
    }

    /// <summary>
    /// Adds webhook-specific resilience (faster retry, shorter timeout).
    /// </summary>
    public static IHttpClientBuilder AddWebhookResilienceHandler(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("webhook", (resilienceBuilder, context) =>
        {
            // Retry for webhooks - fewer retries, faster
            resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome))
            });

            // Shorter timeout for webhooks
            resilienceBuilder.AddTimeout(TimeSpan.FromSeconds(10));
        });

        return builder;
    }

    /// <summary>
    /// Adds email-specific resilience (more retries for transient failures).
    /// </summary>
    public static IHttpClientBuilder AddEmailResilienceHandler(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("email", (resilienceBuilder, context) =>
        {
            // More retries for email - important to deliver
            resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome))
            });

            // Circuit breaker
            resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromMinutes(1),
                FailureRatio = 0.7,
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromMinutes(1),
                ShouldHandle = args => ValueTask.FromResult(ShouldBreak(args.Outcome))
            });

            resilienceBuilder.AddTimeout(TimeSpan.FromSeconds(30));
        });

        return builder;
    }

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            return outcome.Exception is HttpRequestException 
                   or TaskCanceledException 
                   or TimeoutException;
        }

        if (outcome.Result is not null)
        {
            var statusCode = (int)outcome.Result.StatusCode;
            // Retry on 5xx, 408, 429
            return statusCode >= 500 || statusCode == 408 || statusCode == 429;
        }

        return false;
    }

    private static bool ShouldBreak(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            return true;
        }

        if (outcome.Result is not null)
        {
            var statusCode = (int)outcome.Result.StatusCode;
            return statusCode >= 500;
        }

        return false;
    }
}

