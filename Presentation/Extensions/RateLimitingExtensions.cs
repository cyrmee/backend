using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Domain.Constants;

namespace Presentation.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimitingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Default token bucket limiter for all requests
            options.AddTokenBucketLimiter(RateLimitingPolicies.Default, limiterOptions =>
            {
                limiterOptions.TokenLimit = 100;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
                limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(60);
                limiterOptions.TokensPerPeriod = 10;
                limiterOptions.AutoReplenishment = true;
            });

            // Stricter token bucket limiter for login attempts
            options.AddTokenBucketLimiter(RateLimitingPolicies.Login, limiterOptions =>
            {
                limiterOptions.TokenLimit = 5;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
                limiterOptions.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
                limiterOptions.TokensPerPeriod = 1;
                limiterOptions.AutoReplenishment = true;
            });
        });

        return services;
    }

    public static IApplicationBuilder UseRateLimitingServices(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        return app;
    }
}
