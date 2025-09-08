using Domain.Constants;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public static class Seeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Seeder");

        await using var trx = await context.Database.BeginTransactionAsync();
        try
        {
            await SeedRolesAsync(roleManager, logger);
            await SeedUsersAsync(userManager, context, logger);
            await trx.CommitAsync();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Seeding failed; transaction rolled back");
            await trx.RollbackAsync();
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager, ILogger? logger)
    {
        var roles = new[]
        {
            UserRoles.SuperAdmin,
            UserRoles.Admin,
            UserRoles.User,
            UserRoles.Moderator
        };

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;
            var role = new IdentityRole<Guid>(roleName);
            var result = await roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                logger?.LogWarning("Failed to create role {Role}: {Errors}", roleName,
                    string.Join(", ", result.Errors.Select(static e => e.Description)));
                continue;
            }

            logger?.LogInformation("Created role {Role}", roleName);
        }
    }

    private static async Task SeedUsersAsync(UserManager<User> userManager, ApplicationDbContext context,
        ILogger? logger)
    {
        var users = new[]
        {
            new
            {
                Email = "superadmin@backend.net", Password = Pw("SEED_SUPER_ADMIN_PW", "SuperAdmin123!"),
                Role = UserRoles.SuperAdmin
            }
        };

        foreach (var userData in users)
        {
            var user = await userManager.FindByEmailAsync(userData.Email);
            if (user != null) continue;
            user = new User
            {
                UserName = userData.Email,
                Email = userData.Email,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = "seeded",
                UpdatedBy = "seeded"
            };

            var result = await userManager.CreateAsync(user, userData.Password);
            if (!result.Succeeded)
            {
                logger?.LogWarning("Failed to create user {Email}: {Errors}", userData.Email,
                    string.Join(", ", result.Errors.Select(static e => e.Description)));
                continue;
            }

            // Assign role
            await userManager.AddToRoleAsync(user, userData.Role);
            await context.SaveChangesAsync();
            logger?.LogInformation("Seeded user {Email} with role {Role}", userData.Email, userData.Role);
        }
    }

    private static string Pw(string key, string fallback)
    {
        return Environment.GetEnvironmentVariable(key) ?? fallback;
    }
}