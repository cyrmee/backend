using System.Diagnostics;

namespace Presentation.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(context);

        stopwatch.Stop();
        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

        logger.LogInformation("Request {Method} {Path} executed in {ElapsedMilliseconds}ms",
            context.Request.Method,
            context.Request.Path,
            elapsedMilliseconds);
    }
}