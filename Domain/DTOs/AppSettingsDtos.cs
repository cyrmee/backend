using Domain.DTOs.Common;
using Domain.Enums;

namespace Domain.DTOs;

public class AppSettingsDto : BaseDto
{
    public ThemePreference ThemePreference { get; set; }
    public bool Onboarded { get; set; }
    public string UserId { get; set; } = string.Empty;
    public UserBaseDto? User { get; set; }
}

public class AppSettingsBaseDto : BaseDto
{
    public ThemePreference ThemePreference { get; set; }
    public bool Onboarded { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class CreateAppSettingsDto
{
    public ThemePreference ThemePreference { get; set; }
    public bool Onboarded { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class UpdateAppSettingsDto
{
    public ThemePreference? ThemePreference { get; set; }
    public bool? Onboarded { get; set; }
}

public class PaginatedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
