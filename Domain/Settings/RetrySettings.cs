namespace Domain.Settings;

public class RetrySettings
{
    public bool EnableRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public double BaseDelayMs { get; set; } = 2000; // 2 seconds
}