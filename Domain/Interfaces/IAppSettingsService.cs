using Domain.DTOs;

namespace Domain.Interfaces;

public interface IAppSettingsService
{
    Task<PaginatedResult<AppSettingsDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10);
    Task<AppSettingsDto?> GetByIdAsync(Guid id);
    Task<AppSettingsDto?> GetByUserIdAsync(string userId);
    Task<AppSettingsDto> CreateAsync(CreateAppSettingsDto dto);
    Task<AppSettingsDto?> UpdateAsync(Guid id, UpdateAppSettingsDto dto);
    Task<bool> DeleteAsync(Guid id);
}
