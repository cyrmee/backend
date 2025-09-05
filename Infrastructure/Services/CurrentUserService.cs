using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.DTOs.Common;
using Domain.Exceptions;
using Domain.Interfaces;
using Domain.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

public class CurrentUserService(
    IOptions<JwtSettings> jwtSettings
) : ICurrentUserService
{
    public AuthValidationResponse GetCurrentUser(string token)
    {
        // Validate the token locally
        var result = ValidateToken(token, jwtSettings.Value.Secret, out var error);

        if (result == null || !string.IsNullOrEmpty(error))
        {
            throw new UnauthorizedException($"Token validation failed: {error}");
        }

        return result;
    }

    public static AuthValidationResponse? ValidateToken(string token, string secret, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(secret))
        {
            error = "JWT secret is not configured in AppSettings or Jwt.";
            return null;
        }

        try
        {
            var actualToken = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? token["Bearer ".Length..].Trim()
                : token.Trim();
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);

            var principal = tokenHandler.ValidateToken(actualToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var claims = principal.Claims.ToList();
            var userName = claims.FirstOrDefault(static x => x.Type == ClaimTypes.Email)?.Value
                           ?? claims.FirstOrDefault(static x => x.Type == ClaimTypes.NameIdentifier)?.Value
                           ?? string.Empty;

            // Use built-in claims instead of hardcoded strings
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

            var permissions = claims.Where(static x => x.Type == "permissions" ||
                                                     x.Type == "scope" ||
                                                     x.Type == ClaimTypes.AuthorizationDecision)
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
        catch (Exception ex)
        {
            error = ex.Message;
            Console.Error.WriteLine($"Failed to validate token: {token}. Error: {error}");
            return null;
        }
    }
}