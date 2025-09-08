using Application.Services;
using Domain.Interfaces;
using Domain.Settings;
using Infrastructure;
using Infrastructure.Interceptors;
using Infrastructure.Resilience;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Presentation.Filters;
using Serilog;

namespace Presentation.Extensions;

public static class ServiceCollectionExtensions
{
    public static void RegisterAllServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register services in dependency order (infrastructure first, then application, then presentation)
        services = RegisterControllers(services);
        services = RegisterInfrastructureServices(services, configuration);
        services = RegisterApplicationServices(services);
        services.AddLoggingServices(configuration);
        services.AddAuthenticationServices(configuration);
        services = RegisterApiDocumentationServices(services);
        services = RegisterConfigurationServices(services, configuration);
        services = RegisterResilienceServices(services);
        services.AddHealthCheckServices(configuration);
        services.AddHangfireServices(configuration);
        services.AddRateLimitingServices(configuration);
    }

    private static IServiceCollection RegisterControllers(IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            // Global filters for consistent API behavior
            options.Filters.Add<ApiResponseFilter>();
            options.Filters.Add<ModelValidationFilter>();
            options.Filters.Add<UserContextInjectionFilter>();
        });

        return services;
    }

    private static IServiceCollection RegisterInfrastructureServices(IServiceCollection services,
        IConfiguration configuration)
    {
        services = RegisterDataAccessServices(services, configuration);
        services = RegisterCrossCuttingServices(services);

        return services;
    }

    private static IServiceCollection RegisterDataAccessServices(IServiceCollection services,
        IConfiguration configuration)
    {
        // Database context and related services
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly("Infrastructure"));
        });

        return services;
    }

    private static IServiceCollection RegisterCrossCuttingServices(IServiceCollection services)
    {
        // Entity framework interceptors for auditing
        services.AddScoped<ChangeTrackingInterceptor>();
        return services;
    }

    private static IServiceCollection RegisterApplicationServices(IServiceCollection services)
    {
        services = RegisterBusinessServices(services);
        services = RegisterInfrastructureServicesForApplication(services);

        return services;
    }

    private static IServiceCollection RegisterBusinessServices(IServiceCollection services)
    {
        // Core application services implementing domain interfaces
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }

    private static IServiceCollection RegisterInfrastructureServicesForApplication(IServiceCollection services)
    {
        // Services that provide infrastructure capabilities to the application
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }

    private static IServiceCollection RegisterApiDocumentationServices(IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Backend API",
                    Version = "v1",
                    Description = "A secure backend API with JWT authentication and Redis token storage",
                    Contact = new OpenApiContact
                    {
                        Name = "API Support",
                        Email = "cyrmee@gmail.com"
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                };
                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static IServiceCollection RegisterConfigurationServices(IServiceCollection services,
        IConfiguration configuration)
    {
        // Strongly-typed configuration options
        services.Configure<RetrySettings>(configuration.GetSection("RetrySettings"));
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<CircuitBreakerSettings>(configuration.GetSection("CircuitBreakerSettings"));
        services.Configure<SoftDeleteSettings>(configuration.GetSection("SoftDeleteSettings"));

        return services;
    }

    private static IServiceCollection RegisterResilienceServices(IServiceCollection services)
    {
        // Polly policies for resilient HTTP calls
        services.AddSingleton<RetryPolicies>();

        return services;
    }

}

public static class HostBuilderExtensions
{
    public static IHostBuilder UseConfiguredSerilog(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog();
    }
}

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
    {
        // Register all services using the extension method
        builder.Services.RegisterAllServices(builder.Configuration);

        // Configure Serilog for the host
        builder.Host.UseConfiguredSerilog();

        return builder;
    }
}