using System.Diagnostics;
using System.Security.Claims;

namespace Presentation.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await next(context);

        stopwatch.Stop();
        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                    context.User.FindFirst("sub")?.Value ??
                    "anonymous";

        var requestId = context.TraceIdentifier;
        var statusCode = context.Response.StatusCode;
        var method = context.Request.Method;
        var path = context.Request.Path.ToString();
        var queryString = context.Request.QueryString.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        logger.LogInformation(
            "HTTP Request completed {@RequestDetails}",
            new
            {
                RequestId = requestId,
                UserId = userId,
                Method = method,
                Path = path,
                QueryString = queryString,
                StatusCode = statusCode,
                ElapsedMilliseconds = elapsedMilliseconds,
                UserAgent = userAgent,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            });
    }
}