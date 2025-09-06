using System.Security.Claims;
using Domain.Entities;

namespace Domain.Interfaces;

public interface IJwtTokenService
{
    Task<string> GenerateAccessToken(User user);
    Task<string> GenerateRefreshToken(User user, TimeSpan lifetime);
    ClaimsPrincipal ValidateToken(string token, bool validateLifetime = true);
}
