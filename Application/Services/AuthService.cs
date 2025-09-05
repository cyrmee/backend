using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.DTOs;
using Domain.Models;
using Domain.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Domain.Interfaces;

namespace Application.Services;

public class AuthService(
	UserManager<User> userManager,
	IOptions<JwtSettings> jwtOptions,
	ITokenStoreService tokenStore
) : IAuthService
{
	private readonly JwtSettings _jwt = jwtOptions.Value;

	public async Task<JwtAuthResponseDto> RegisterAsync(RegisterDto dto)
	{
		var existing = await userManager.FindByEmailAsync(dto.Email);
		if (existing != null)
			throw new InvalidOperationException("User with this email already exists");

		var user = new User
		{
			UserName = dto.Email,
			Email = dto.Email,
		};
		var result = await userManager.CreateAsync(user, dto.Password);
		if (!result.Succeeded)
		{
			var error = string.Join(", ", result.Errors.Select(e => e.Description));
			throw new InvalidOperationException($"Failed to register user: {error}");
		}

		await userManager.UpdateAsync(user);

		return await CreateAuthResponseAsync(user);
	}

	public async Task<JwtAuthResponseDto> LoginAsync(LoginDto dto)
	{
		var user = await userManager.FindByEmailAsync(dto.Email);
		if (user == null)
			throw new UnauthorizedAccessException("Invalid credentials");

		var passwordValid = await userManager.CheckPasswordAsync(user, dto.Password);
		if (!passwordValid)
			throw new UnauthorizedAccessException("Invalid credentials");

		user.LastLoginAt = DateTime.UtcNow;
		await userManager.UpdateAsync(user);

		return await CreateAuthResponseAsync(user);
	}

	public async Task<JwtAuthResponseDto> RefreshAccessTokenAsync(string refreshToken)
	{
		// Validate token and ensure type == refresh and jti exists in Redis
		var handler = new JwtSecurityTokenHandler();
		var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
			ValidateIssuer = !string.IsNullOrEmpty(_jwt.Issuer),
			ValidIssuer = _jwt.Issuer,
			ValidateAudience = !string.IsNullOrEmpty(_jwt.Audience),
			ValidAudience = _jwt.Audience,
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero
		}, out _);

		var type = principal.Claims.FirstOrDefault(c => c.Type == "type")?.Value;
		var sub = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
		var jti = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti || c.Type == "jti")?.Value;
		if (type != "refresh" || string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(jti))
			throw new UnauthorizedAccessException("Invalid refresh token");

		var refreshKeyActive = await tokenStore.IsRefreshJtiActiveAsync(sub, jti);
		if (!refreshKeyActive)
			throw new UnauthorizedAccessException("Refresh token not found or expired");

		var user = await userManager.FindByIdAsync(sub);
		if (user == null)
			throw new UnauthorizedAccessException("User not found");

		// rotate refresh token: delete old and create new
		await tokenStore.RevokeRefreshJtiAsync(sub, jti);
		return await CreateAuthResponseAsync(user);
	}

	public async Task LogoutAsync(string? refreshToken, string? userId)
	{
		string? sub = userId;
		if (!string.IsNullOrEmpty(refreshToken))
		{
			try
			{
				var handler = new JwtSecurityTokenHandler();
				var decoded = handler.ReadJwtToken(refreshToken);
				sub = decoded.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value ?? sub;
				var jti = decoded.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti || c.Type == "jti")?.Value;
				if (!string.IsNullOrEmpty(sub) && !string.IsNullOrEmpty(jti))
					await tokenStore.RevokeRefreshJtiAsync(sub, jti);
			}
			catch { /* ignore */ }
		}

		if (!string.IsNullOrEmpty(sub))
			await tokenStore.RevokeAllForUserAsync(sub);
	}

	private async Task<JwtAuthResponseDto> CreateAuthResponseAsync(User user)
	{
		var accessJti = Guid.NewGuid().ToString("N");
		var refreshJti = Guid.NewGuid().ToString("N");

		var accessToken = GenerateToken(user, accessJti, "access", TimeSpan.FromMinutes(_jwt.ExpirationMinutes));
		var refreshToken = GenerateToken(user, refreshJti, "refresh", TimeSpan.FromDays(_jwt.RefreshTokenExpirationDays));

		// store JTIs in redis with TTL
	await tokenStore.StoreAccessJtiAsync(user.Id, accessJti, TimeSpan.FromMinutes(_jwt.ExpirationMinutes));
	await tokenStore.StoreRefreshJtiAsync(user.Id, refreshJti, TimeSpan.FromDays(_jwt.RefreshTokenExpirationDays));

		var userDto = new AuthUserResponseDto
		{
			Id = user.Id,
			Email = user.Email ?? string.Empty,
			Name = user.UserName ?? string.Empty,
			IsActive = true,
			IsVerified = true,
		};

		return new JwtAuthResponseDto
		{
			AccessToken = accessToken,
			RefreshToken = refreshToken,
			User = userDto
		};
	}

	private string GenerateToken(User user, string jti, string type, TimeSpan lifetime)
	{
		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, user.Id),
			new(JwtRegisteredClaimNames.Jti, jti),
			new("type", type),
			new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
			new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
			new("isActive", "true"),
			new("isVerified", "true"),
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var token = new JwtSecurityToken(
			issuer: string.IsNullOrEmpty(_jwt.Issuer) ? null : _jwt.Issuer,
			audience: string.IsNullOrEmpty(_jwt.Audience) ? null : _jwt.Audience,
			claims: claims,
			expires: DateTime.UtcNow.Add(lifetime),
			signingCredentials: creds
		);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
