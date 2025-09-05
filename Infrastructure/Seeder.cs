using Domain.Constants;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class Seeder
{
	public static async Task SeedAsync(IServiceProvider serviceProvider)
	{
		using var scope = serviceProvider.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
		var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

		await SeedRolesAsync(roleManager);
		await SeedUsersAsync(userManager, context);
	}

	private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
	{
		var roles = new[]
		{
			RoleConstants.Admin,
			RoleConstants.User,
			RoleConstants.Moderator,
		};

		foreach (var roleName in roles)
		{
			if (await roleManager.RoleExistsAsync(roleName)) continue;
			var role = new IdentityRole(roleName);
			var result = await roleManager.CreateAsync(role);

			if (!result.Succeeded)
			{
				throw new Exception(
					$"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(static e => e.Description))}");
			}
		}
	}

	private static async Task SeedUsersAsync(UserManager<User> userManager, ApplicationDbContext context)
	{
		var users = new[]
		{
			new
			{
				Email = "admin@example.com", Password = "Admin123!", Role = RoleConstants.Admin,
				Theme = ThemePreference.Dark
			},
			new
			{
				Email = "user@example.com", Password = "User123!", Role = RoleConstants.User,
				Theme = ThemePreference.Light
			},
			new
			{
				Email = "moderator@example.com", Password = "Moderator123!", Role = RoleConstants.Moderator,
				Theme = ThemePreference.System
			},
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
				throw new Exception(
					$"Failed to create user '{userData.Email}': {string.Join(", ", result.Errors.Select(static e => e.Description))}");
			}

			// Assign role
			await userManager.AddToRoleAsync(user, userData.Role);

			// Create AppSettings
			var appSettings = new AppSettings
			{
				UserId = user.Id,
				ThemePreference = userData.Theme,
				Onboarded = true,
				User = user,
				CreatedBy = "seeded",
				UpdatedBy = "seeded"
			};

			await context.AppSettings.AddAsync(appSettings);
			await context.SaveChangesAsync();
		}
	}
}