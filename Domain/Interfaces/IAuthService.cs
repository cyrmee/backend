using Domain.Models;

namespace Domain.Interfaces;

public interface IAuthService
{
    Task<JwtAuthResponseModel> RegisterAsync(RegisterModel model);
    Task<JwtAuthResponseModel> LoginAsync(LoginModel model);
    Task<JwtAuthResponseModel> RefreshAccessTokenAsync(string refreshToken);
    Task LogoutAsync(LogoutModel model);
    Task LogoutAllSessionsAsync(string userId);
}