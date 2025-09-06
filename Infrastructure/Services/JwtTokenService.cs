using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Constants;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

public class JwtTokenService(IOptions<JwtSettings> jwtOptions, ITokenStoreService tokenStore) : IJwtTokenService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task<string> GenerateAccessToken(User user)
    {
        var jti = Guid.NewGuid().ToString("N");
        var token = GenerateToken(user, jti, TokenTypes.Access, TimeSpan.FromMinutes(_jwt.ExpirationMinutes));
        await tokenStore.StoreAccessJtiAsync(user.Id.ToString(), jti, TimeSpan.FromMinutes(_jwt.ExpirationMinutes));
        return token;
    }

    public async Task<string> GenerateRefreshToken(User user, TimeSpan lifetime)
    {
        var jti = Guid.NewGuid().ToString("N");
        var token = GenerateToken(user, jti, TokenTypes.Refresh, lifetime);
        await tokenStore.StoreRefreshJtiAsync(user.Id.ToString(), jti, lifetime);
        return token;
    }

    public ClaimsPrincipal ValidateToken(string token, bool validateLifetime = true)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.Audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.Zero
        };
        return handler.ValidateToken(token, parameters, out _);
    }

    private string GenerateToken(User user, string jti, string type, TimeSpan lifetime)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.Name ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.UserName ?? string.Empty),
            new(TokenTypes.TokenTypeClaim, type)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _jwt.Issuer,
            _jwt.Audience,
            claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
