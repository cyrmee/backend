using Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Infrastructure.Resilience;

public class RetryPolicies(
    IOptions<RetrySettings> retryOptions,
    IOptions<CircuitBreakerSettings> circuitBreakerOptions,
    ILogger<RetryPolicies> logger)
{
    private readonly CircuitBreakerSettings _circuitBreakerSettings = circuitBreakerOptions.Value;
    private readonly RetrySettings _retrySettings = retryOptions.Value;

    private static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            TimeoutException => true,
            TaskCanceledException => true, // includes client timeouts
            HttpRequestException => true,
            NpgsqlException { IsTransient: true } => true,
            _ => false
        };
    }

    private AsyncRetryPolicy<T> CreateRetryPolicy<T>()
    {
        if (_retrySettings.MaxRetries <= 0)
            // Create a single-attempt retry (no retries) policy to satisfy return type without null casts
            return Policy<T>
                .Handle<Exception>(_ => false)
                .WaitAndRetryAsync(1, _ => TimeSpan.Zero);

        var maxRetries = _retrySettings.MaxRetries;
        var baseDelay = Math.Max(50, _retrySettings.BaseDelayMs);

        return Policy<T>
            .Handle<Exception>(IsTransient)
            .WaitAndRetryAsync(
                maxRetries,
                attempt =>
                {
                    var expo = Math.Pow(2, attempt - 1);
                    var jitter = Random.Shared.NextDouble() * 0.25 + 0.75; // 0.75 - 1.0 range
                    var delayMs = baseDelay * expo * jitter;
                    var capped = Math.Min(delayMs, baseDelay * Math.Pow(2, maxRetries - 1) * 1.5);
                    return TimeSpan.FromMilliseconds(capped);
                },
                (outcome, timeSpan, retryCount, _) =>
                {
                    logger.LogWarning(outcome.Exception,
                        "Transient failure (attempt {Attempt}/{Max}). Retrying in {DelayMs} ms: {Message}",
                        retryCount, maxRetries, (int)timeSpan.TotalMilliseconds, outcome.Exception?.Message);
                });
    }

    private AsyncCircuitBreakerPolicy<T> GetCircuitBreakerPolicy<T>()
    {
        return Policy<T>
            .Handle<Exception>(IsTransient)
            .CircuitBreakerAsync(
                Math.Max(1, _circuitBreakerSettings.ExceptionsAllowedBeforeBreaking),
                TimeSpan.FromSeconds(Math.Max(1, _circuitBreakerSettings.DurationOfBreakSeconds))
            );
    }

    public async Task<T> ExecuteWithRetryAndCircuitBreakerAsync<T>(Func<Task<T>> action)
    {
        var breaker = GetCircuitBreakerPolicy<T>();

        if (!_retrySettings.EnableRetry)
            return await breaker.ExecuteAsync(action);

        var retry = CreateRetryPolicy<T>();
        var policyWrap = Policy.WrapAsync(retry, breaker);
        return await policyWrap.ExecuteAsync(action);
    }
}