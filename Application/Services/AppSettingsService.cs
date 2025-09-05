using Application.Common;
using Domain.DTOs;
using Domain.Interfaces;
using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class AppSettingsService(
	ApplicationDbContext context
) : IAppSettingsService
{
	public async Task<PaginatedResult<AppSettingsDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10)
	{
		var query = context.AppSettings.AsQueryable();
		var totalCount = await query.CountAsync();

		var items = await query
			.Include(x => x.User)
			.OrderByDescending(x => x.CreatedAt)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		var dtos = items.Select(item => Mapper.Map<AppSettings, AppSettingsDto>(item))
			.ToList();

		return new PaginatedResult<AppSettingsDto>
		{
			Items = dtos,
			PageNumber = pageNumber,
			PageSize = pageSize,
			TotalCount = totalCount
		};
	}

	public async Task<AppSettingsDto?> GetByIdAsync(Guid id)
	{
		var appSettings = await context.AppSettings
			.FirstOrDefaultAsync(x => x.Id == id);

		return appSettings == null ? null : Mapper.Map<AppSettings, AppSettingsDto>(appSettings);
	}

	public async Task<AppSettingsDto?> GetByUserIdAsync(string userId)
	{
		var appSettings = await context.AppSettings
			.FirstOrDefaultAsync(x => x.UserId == userId);

		return appSettings == null ? null : Mapper.Map<AppSettings, AppSettingsDto>(appSettings);
	}

	public async Task<AppSettingsDto> CreateAsync(CreateAppSettingsDto dto)
	{
		var appSettings = Mapper.Map<CreateAppSettingsDto, AppSettings>(dto);
		appSettings.Id = Guid.NewGuid();
		appSettings.CreatedAt = DateTime.UtcNow;
		appSettings.UpdatedAt = DateTime.UtcNow;

		context.AppSettings.Add(appSettings);
		await context.SaveChangesAsync();

		return Mapper.Map<AppSettings, AppSettingsDto>(appSettings);
	}

	public async Task<AppSettingsDto?> UpdateAsync(Guid id, UpdateAppSettingsDto dto)
	{
		var appSettings = await context.AppSettings.FindAsync(id);
		if (appSettings == null)
			return null;

		if (dto.ThemePreference.HasValue)
			appSettings.ThemePreference = dto.ThemePreference.Value;

		if (dto.Onboarded.HasValue)
			appSettings.Onboarded = dto.Onboarded.Value;

		appSettings.UpdatedAt = DateTime.UtcNow;

		await context.SaveChangesAsync();

		return Mapper.Map<AppSettings, AppSettingsDto>(appSettings);
	}

	public async Task<bool> DeleteAsync(Guid id)
	{
		var appSettings = await context.AppSettings.FindAsync(id);
		if (appSettings == null)
			return false;

		context.AppSettings.Remove(appSettings);
		await context.SaveChangesAsync();

		return true;
	}
}