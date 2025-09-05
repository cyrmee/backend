using Domain.DTOs;

namespace Domain.Interfaces;

public interface IAuthService
{
    Task<JwtAuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<JwtAuthResponseDto> LoginAsync(LoginDto dto);
    Task<JwtAuthResponseDto> RefreshAccessTokenAsync(string refreshToken);
    Task LogoutAsync(string? refreshToken, string? userId);
}
