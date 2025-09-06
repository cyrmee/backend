using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Interfaces;
using Domain.Models;
using Domain.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class AuthService(
    UserManager<User> userManager,
    IOptions<JwtSettings> jwtOptions,
    ITokenStoreService tokenStore,
    IJwtTokenService jwtTokenService,
    ILogger<AuthService> logger
) : IAuthService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public async Task<JwtAuthResponseModel> RegisterAsync(RegisterModel model)
    {
        var existing = await userManager.FindByEmailAsync(model.Email);
        if (existing != null)
            throw new ConflictException("User with this email already exists");

        var user = Mapper.Map<RegisterModel, User>(model);

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            var error = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BadRequestException($"Failed to register user: {error}");
        }

        await userManager.AddToRoleAsync(user, Roles.User);
        await userManager.UpdateAsync(user);

        return await CreateAuthResponseAsync(user);
    }

    public async Task<JwtAuthResponseModel> LoginAsync(LoginModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email)
                   ?? throw new UnauthorizedException("Invalid credentials");

        var passwordValid = await userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordValid)
            throw new UnauthorizedException("Invalid credentials");

        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        return await CreateAuthResponseAsync(user, model.RememberMe);
    }

    public async Task<JwtAuthResponseModel> RefreshAccessTokenAsync(string refreshToken)
    {
        // Validate token and ensure type == refresh and jti exists in Redis
        var principal = jwtTokenService.ValidateToken(refreshToken, validateLifetime: true);

        var type = principal.Claims.FirstOrDefault(c => c.Type == TokenTypes.TokenTypeClaim)?.Value;
        var sub = principal.Claims
            .FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Sub or ClaimTypes.NameIdentifier)?.Value;
        var jti = principal.Claims.FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Jti)?.Value;
        if (type != TokenTypes.Refresh || string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(jti))
            throw new UnauthorizedException("Invalid refresh token");

        var refreshKeyActive = await tokenStore.IsRefreshJtiActiveAsync(sub, jti);
        if (!refreshKeyActive)
            throw new UnauthorizedException("Refresh token not found or expired");

        var user = await userManager.FindByIdAsync(sub)
            ?? throw new UnauthorizedException("User not found");

        // rotate refresh token: delete old and create new
        await tokenStore.RevokeRefreshJtiAsync(sub, jti);
        return await CreateAuthResponseAsync(user);
    }

    public async Task LogoutAsync(LogoutModel model)
    {
        var sub = model.UserId;
        if (!string.IsNullOrEmpty(model.RefreshToken))
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var decoded = handler.ReadJwtToken(model.RefreshToken);
                sub = decoded.Claims
                          .FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Sub or ClaimTypes.NameIdentifier)
                          ?.Value ??
                      sub;
                var jti = decoded.Claims.FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(sub) && !string.IsNullOrEmpty(jti))
                    await tokenStore.RevokeRefreshJtiAsync(sub, jti);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while logging out refresh token");
            }

        if (!string.IsNullOrEmpty(model.AccessToken))
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var decoded = handler.ReadJwtToken(model.AccessToken);
                sub = decoded.Claims
                          .FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Sub or ClaimTypes.NameIdentifier)
                          ?.Value ??
                      sub;
                var jti = decoded.Claims.FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(sub) && !string.IsNullOrEmpty(jti))
                    await tokenStore.RevokeAccessJtiAsync(sub, jti);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while logging out access token");
            }
    }

    public async Task LogoutAllSessionsAsync(string userId) =>
        await tokenStore.RevokeAllForUserAsync(userId);

    private async Task<JwtAuthResponseModel> CreateAuthResponseAsync(User user, bool rememberMe = false)
    {
        var accessToken = await jwtTokenService.GenerateAccessToken(user);

        var refreshDays = rememberMe ? _jwt.RefreshTokenExpirationDays : _jwt.RefreshTokenExpirationDays + 6;
        var refreshLifetime = TimeSpan.FromDays(refreshDays);
        var refreshToken = await jwtTokenService.GenerateRefreshToken(user, refreshLifetime);

        var authUserResponse = Mapper.Map<User, UserModel>(user);

        return new JwtAuthResponseModel
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = authUserResponse
        };
    }
}