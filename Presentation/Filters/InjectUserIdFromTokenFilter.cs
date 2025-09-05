using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using Domain.Exceptions;
using Domain.Interfaces;

namespace Presentation.Filters;

public class InjectUserIdFromTokenFilter(ICurrentUserService currentUserService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Only run for authenticated requests
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            await next();
            return;
        }

        var httpContext = context.HttpContext;
        var token = httpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        var user = currentUserService.GetCurrentUser(token);

        if (user is { UserId: not null })
        {
            // Set globally for controller access
            context.HttpContext.Items[ClaimTypes.NameIdentifier] = user.UserId;
            if (!string.IsNullOrEmpty(user.Email))
                context.HttpContext.Items[ClaimTypes.Email] = user.Email;

            // Still inject into DTO properties if present
            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg == null) continue;
                // Inject UserId
                var userIdProp = arg.GetType().GetProperty("UserId");
                if (userIdProp != null && userIdProp.CanWrite && userIdProp.PropertyType == typeof(Guid))
                    userIdProp.SetValue(arg, user.UserId);

                // Inject Email
                var emailProp = arg.GetType().GetProperty("Email");
                if (emailProp == null || !emailProp.CanWrite || emailProp.PropertyType != typeof(string) ||
                    user.Email is null) continue;
                emailProp.SetValue(arg, user.Email);
            }
        }
        else
        {
            throw new UnauthorizedException("UserId not found in token or context.");
        }

        await next();
    }
}
