namespace Domain.Settings;

public class JwtSettings
{
    public required string Secret { get; set; } = string.Empty;
    public required string Issuer { get; set; } = string.Empty;
    public required string Audience { get; set; } = string.Empty;
    public required int ExpirationMinutes { get; set; } = 15;
    public required int RefreshTokenExpirationDays { get; set; } = 1;
}