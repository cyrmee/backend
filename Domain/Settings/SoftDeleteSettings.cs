namespace Domain.Settings;

public class SoftDeleteSettings
{
    public required int RetentionDays { get; set; } = 15;
    public required int BatchSize { get; set; } = 500;
}
