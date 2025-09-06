using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Domain.Exceptions;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Presentation.Filters;

public class UserContextInjectionFilter(ICurrentUserService currentUserService) : IAsyncActionFilter
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
            context.HttpContext.Items[JwtRegisteredClaimNames.Sub] = user.UserId;
            if (!string.IsNullOrEmpty(user.Email))
                context.HttpContext.Items[ClaimTypes.Email] = user.Email;

            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg == null) continue;
                // Inject UserId
                var userIdProp = arg.GetType().GetProperty("UserId");
                if (userIdProp != null && userIdProp.CanWrite)
                {
                    if (userIdProp.PropertyType == typeof(Guid))
                        userIdProp.SetValue(arg, user.UserId);
                    else if (userIdProp.PropertyType == typeof(string))
                        userIdProp.SetValue(arg, user.UserId.ToString());
                }

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