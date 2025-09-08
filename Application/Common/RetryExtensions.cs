namespace Application.Common;

public static class RetryExtensions
{
    public static async Task<T> WithRetryAsync<T>(
        this Func<Task<T>> action,
        int maxRetries = 3,
        Func<Exception, bool>? shouldRetry = null)
    {
        shouldRetry ??= ex => ex is TimeoutException || ex is HttpRequestException;

        Exception? lastException = null;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxRetries && shouldRetry(ex))
            {
                lastException = ex;
                var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                await Task.Delay(delay);
            }
        }

        throw lastException ?? new InvalidOperationException("Retry failed without exception");
    }
}
