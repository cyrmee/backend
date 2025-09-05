using Domain.Settings;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Infrastructure.Resilience;

public class RetryPolicies(IOptions<RetrySettings> retryOptions, IOptions<CircuitBreakerSettings> circuitBreakerOptions)
{
	private readonly RetrySettings _retrySettings = retryOptions.Value;
	private readonly CircuitBreakerSettings _circuitBreakerSettings = circuitBreakerOptions.Value;

	private AsyncRetryPolicy<T> CreateRetryPolicy<T>()
	{
		return Policy<T>
			.Handle<Exception>()
			.WaitAndRetryAsync(
				Math.Max(0, _retrySettings.MaxRetries),
				attempt => TimeSpan.FromMilliseconds(
					Math.Max(50, _retrySettings.BaseDelayMs) * Math.Pow(2, attempt - 1)),
				(outcome, timeSpan, retryCount, _) =>
				{
					Console.WriteLine(
						$"Retry {retryCount} after {timeSpan.TotalMilliseconds}ms due to: {outcome.Exception?.Message}");
				});
	}

	private AsyncCircuitBreakerPolicy<T> GetCircuitBreakerPolicy<T>()
	{
		return Policy<T>
			.Handle<Exception>()
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