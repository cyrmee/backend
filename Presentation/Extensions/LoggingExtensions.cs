using Serilog;
using Serilog.Formatting.Json;

namespace Presentation.Extensions;

public static class LoggingExtensions
{
    public static IServiceCollection AddLoggingServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Serilog
        var appName = configuration["Serilog:Properties:Application"] ?? "Backend";
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var logsPath = Path.Combine(userHome, appName, "logs");

        // Ensure logs directory exists
        try
        {
            Directory.CreateDirectory(logsPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create logs directory at {logsPath}: {ex.Message}");
            logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsPath);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", appName)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logsPath, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logsPath, "log-structured-.json"),
                rollingInterval: RollingInterval.Day,
                formatter: new JsonFormatter())
            .CreateLogger();

        services.AddLogging();
        return services;
    }
}
