using System.Security.Claims;
using Domain.Exceptions;
using Domain.Interfaces;
using Domain.Models.Common;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class CurrentUserService(
    IJwtTokenService jwtTokenService,
    ILogger<CurrentUserService> logger
) : ICurrentUserService
{
    public AuthValidationResponse GetCurrentUser(string token)
    {
        try
        {
            var actualToken = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? token["Bearer ".Length..].Trim()
                : token.Trim();
            var principal = jwtTokenService.ValidateToken(actualToken, validateLifetime: true);
            return MapPrincipal(principal);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate token");
            throw new UnauthorizedException("Token validation failed");
        }
    }

    private static AuthValidationResponse MapPrincipal(ClaimsPrincipal principal)
    {
        var claims = principal.Claims.ToList();
        var userName = claims.FirstOrDefault(static x => x.Type == ClaimTypes.Email)?.Value
                       ?? claims.FirstOrDefault(static x => x.Type == ClaimTypes.NameIdentifier)?.Value
                       ?? string.Empty;

        var userIdStr =
            claims.FirstOrDefault(static x => x.Type == ClaimTypes.NameIdentifier)?.Value
            ?? claims.FirstOrDefault(static x => x.Type == "sub")?.Value
            ?? string.Empty;

        Guid? userId = null;
        if (Guid.TryParse(userIdStr, out var parsedGuid))
            userId = parsedGuid;

        var roles = claims.Where(static x => x.Type == ClaimTypes.Role)
            .Select(static x => x.Value)
            .ToList();

        var permissions = claims
            .Where(static x => x.Type is "permissions" or "scope" or ClaimTypes.AuthorizationDecision)
            .Select(static x => x.Value).ToList();

        return new AuthValidationResponse
        {
            UserName = userName,
            UserId = userId,
            Roles = roles,
            Permissions = permissions,
            Email = claims.FirstOrDefault(static x => x.Type == ClaimTypes.Email)?.Value
                    ?? claims.FirstOrDefault(static x => x.Type == "email")?.Value
                    ?? string.Empty
        };
    }
}