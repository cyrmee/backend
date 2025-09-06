namespace Domain.Settings;

public class CircuitBreakerSettings
{
    public int ExceptionsAllowedBeforeBreaking { get; set; } = 3;
    public int DurationOfBreakSeconds { get; set; } = 30;
}