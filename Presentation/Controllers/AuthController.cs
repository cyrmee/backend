using Domain.Interfaces;
using Domain.Models;
using Domain.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;
using System.Security.Claims;

namespace Presentation.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(
    IAuthService authService,
    ILogger<AuthController> logger
) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<JwtAuthResponseModel>>> Register([FromBody] RegisterModel model)
    {
        var result = await authService.RegisterAsync(model);
        return result.ToResponse();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<JwtAuthResponseModel>>> Login([FromBody] LoginModel model)
    {
        var result = await authService.LoginAsync(model);
        return result.ToResponse();
    }

    [HttpPost("refresh-access-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ResponseModel<JwtAuthResponseModel>>> Refresh([FromBody] RefreshTokenRequest dto)
    {
        var result = await authService.RefreshAccessTokenAsync(dto.RefreshToken);
        return result.ToResponse();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<object>>> Logout([FromBody] LogoutModel model)
    {
        model.AccessToken ??= Request.Headers.Authorization.ToString()
            .Replace("Bearer ", "");
        logger.LogCritical("Refresh Token on Logout: {AccessToken}", model.AccessToken);
        logger.LogCritical("Refresh Token on Logout: {RefreshToken}", model.RefreshToken);
        logger.LogCritical("User ID on Logout: {UserId}", model.UserId);
        await authService.LogoutAsync(model);
        var payload = new { message = "Logged out successfully" } as object;
        return payload.ToResponse();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<object>>> LogoutAllSessions()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return BadRequest("User ID not found");

        await authService.LogoutAllSessionsAsync(userId);
        var payload = new { message = "All sessions logged out successfully" } as object;
        return payload.ToResponse();
    }
}