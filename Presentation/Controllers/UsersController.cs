using System.Security.Claims;
using Domain.Exceptions;
using Domain.Interfaces;
using Domain.Models;
using Domain.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet("get-all")]
    [Authorize(Roles = "Admin")]
    public async Task<ResponseModel<List<UserModel>>> GetAll([FromQuery] UserQuery query,
        CancellationToken cancellationToken)
    {
        var paged = await userService.GetUsersPaginatedAsync(query, cancellationToken);
        var pagination = new Pagination
        {
            Page = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
        return paged.Data.ToResponse(pagination);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<UserModel>>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await userService.GetUserAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(UserModel), id);

        return user.ToResponse();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ResponseModel<UserModel>>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(idClaim) && HttpContext.Items[ClaimTypes.NameIdentifier] is Guid g)
            idClaim = g.ToString();

        if (!Guid.TryParse(idClaim, out var userId))
            throw new UnauthorizedException();

        var user = await userService.GetUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(UserModel), userId);

        return user.ToResponse();
    }
}