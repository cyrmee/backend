using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await context.Users
            .Include(u => u.AppSettings)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.CreatedAt,
                AppSettings = u.AppSettings == null
                    ? null
                    : new
                    {
                        u.AppSettings.ThemePreference,
                        u.AppSettings.Onboarded
                    }
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await context.Users
            .Include(u => u.AppSettings)
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.CreatedAt,
                AppSettings = u.AppSettings == null
                    ? null
                    : new
                    {
                        u.AppSettings.ThemePreference,
                        u.AppSettings.Onboarded
                    }
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }
}