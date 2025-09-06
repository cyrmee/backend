using System.Security.Claims;
using System.Text;
using Application.Services;
using Domain.Constants;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Settings;
using Infrastructure;
using Infrastructure.Interceptors;
using Infrastructure.Resilience;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Presentation.Filters;
using StackExchange.Redis;

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
        services = RegisterLoggingServices(services);
        services = RegisterAuthenticationServices(services, configuration);
        services = RegisterApiDocumentationServices(services);
        services = RegisterConfigurationServices(services, configuration);
        services = RegisterResilienceServices(services);
        RegisterHangfireServices(services, configuration);
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

    private static IServiceCollection RegisterAuthenticationServices(IServiceCollection services,
        IConfiguration configuration)
    {
        // ASP.NET Core Identity for user management
        services.AddIdentity<User, IdentityRole<Guid>>(options =>
            {
                // Password security requirements
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;

                // Account lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // JWT Authentication (Access/Refresh with JTI)
        var jwtSection = configuration.GetSection("JwtSettings");
        var jwtSettings = jwtSection.Get<JwtSettings>();
        if (jwtSettings == null)
            throw new InvalidOperationException("JwtSettings section is not configured properly.");

        byte[] keyBytes;
        var secret = jwtSettings.Secret;
        if (secret.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            var b64 = secret[7..].Trim();
            try
            {
                keyBytes = Convert.FromBase64String(b64);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("JwtSettings.Secret base64 value is invalid.", ex);
            }
        }
        else
        {
            keyBytes = Encoding.UTF8.GetBytes(secret);
        }

        // Enforce minimum key size for HS256 (>= 256 bits / 32 bytes)
        if (keyBytes.Length < 32)
            throw new InvalidOperationException(
                $"JwtSettings.Secret is too short for HS256. Provided {keyBytes.Length * 8} bits; required >= 256 bits. Use a 32+ byte (e.g., 64+ char) random secret or a base64 string prefixed with 'base64:'.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = !string.IsNullOrEmpty(jwtSettings.Issuer),
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = !string.IsNullOrEmpty(jwtSettings.Audience),
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Hook to enforce JTI revocation via Redis for access tokens
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal;
                        if (principal is null)
                        {
                            context.Fail("Invalid token principal");
                            return Task.CompletedTask;
                        }

                        // Expect 'type' claim == 'access' and 'jti' present
                        var claims = principal.Claims.ToList();
                        var tokenType = claims.FirstOrDefault(c => c.Type == "type")?.Value;
                        var jti = claims.FirstOrDefault(c => c.Type == "jti")?.Value;
                        var sub = claims.FirstOrDefault(c =>
                            c.Type is "sub" or ClaimTypes.NameIdentifier)?.Value;

                        if (tokenType != TokenTypes.Access || string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(sub))
                        {
                            context.Fail("Invalid token format");
                            return Task.CompletedTask;
                        }

                        // Check Redis for access_jti:{userId}:{jti}
                        var tokenStore = context.HttpContext.RequestServices.GetService<ITokenStoreService>();
                        if (tokenStore == null)
                        {
                            context.Fail("Token store not configured");
                            return Task.CompletedTask;
                        }

                        var exists = tokenStore.IsAccessJtiActiveAsync(sub, jti).GetAwaiter().GetResult();
                        if (!exists) context.Fail("Token revoked or expired");

                        return Task.CompletedTask;
                    }
                };
            });

        var redisConnString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrEmpty(redisConnString))
            throw new InvalidOperationException("Redis connection string is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var attempts = 0;
            Exception? last = null;
            var delay = TimeSpan.FromMilliseconds(200);
            while (attempts < 3)
            {
                try
                {
                    var mux = ConnectionMultiplexer.Connect(redisConnString);
                    if (mux.IsConnected) return mux;
                }
                catch (Exception ex)
                {
                    last = ex;
                }

                attempts++;
                Thread.Sleep(delay);
                // Exponential backoff with cap
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 2000));
            }

            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                $"Could not connect to Redis after {attempts} attempts: {last?.Message}");
        });

        services.AddScoped<ITokenStoreService>(sp =>
        {
            var mux = sp.GetRequiredService<IConnectionMultiplexer>();
            return mux.IsConnected
                ? new RedisTokenStoreService(mux)
                : throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis not connected");
        });

        // Basic health check style registration (if HealthChecks added later)
        services.AddOptions();

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

        return services;
    }

    private static IServiceCollection RegisterResilienceServices(IServiceCollection services)
    {
        // Polly policies for resilient HTTP calls
        services.AddSingleton<RetryPolicies>();

        return services;
    }

    private static IServiceCollection RegisterLoggingServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConfiguration(new ConfigurationBuilder().AddInMemoryCollection().Build().GetSection("Logging"));
            builder.AddConsole();
        });

        return services;
    }

    // Last registration not returning IServiceCollection
    private static void RegisterHangfireServices(IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHangfireServices(configuration);
    }
}