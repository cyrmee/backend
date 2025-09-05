using Domain.Enums;

namespace Domain.Models;

public class AppSettings : BaseEntity
{
    public ThemePreference? ThemePreference { get; set; }
    public bool Onboarded { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}
