using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Domain.Interfaces;
using Domain.Models;
using Domain.Settings;
using Infrastructure;
using Infrastructure.Interceptors;
using Infrastructure.Resilience;
using Infrastructure.Services;
using Presentation.Filters;
using Presentation.Swagger;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using Microsoft.OpenApi.Models;

namespace Presentation.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection RegisterAllServices(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Register services in dependency order (infrastructure first, then application, then presentation)
		services = RegisterControllers(services);
		services = RegisterInfrastructureServices(services, configuration);
		services = RegisterApplicationServices(services);
		services = RegisterLoggingServices(services);
		services = RegisterWebFrameworkServices(services);
		services = RegisterAuthenticationServices(services, configuration);
		services = RegisterApiDocumentationServices(services);
		services = RegisterConfigurationServices(services, configuration);
		services = RegisterResilienceServices(services);

		return services;
	}

	private static IServiceCollection RegisterControllers(IServiceCollection services)
	{
		services.AddControllers(options =>
		{
			// Global filters for consistent API behavior
			options.Filters.Add<ResponseFormatFilter>();
			options.Filters.Add<ModelValidationFilter>();
			options.Filters.Add<InjectUserIdFromTokenFilter>();
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
		services.AddScoped<AuditableEntityInterceptor>();

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
		services.AddScoped<IAppSettingsService, Application.Services.AppSettingsService>();
		services.AddScoped<IAuthService, Application.Services.AuthService>();

		return services;
	}

	private static IServiceCollection RegisterInfrastructureServicesForApplication(IServiceCollection services)
	{
		// Services that provide infrastructure capabilities to the application
		services.AddScoped<ICurrentUserService, CurrentUserService>();
		services.AddScoped<IEmailService, EmailService>();

		return services;
	}

	private static IServiceCollection RegisterWebFrameworkServices(IServiceCollection services)
	{
		// ASP.NET Core MVC and API configuration
		services.AddControllers(options =>
		{
			// Global filters for consistent API behavior
			options.Filters.Add<ResponseFormatFilter>();
			options.Filters.Add<ModelValidationFilter>();
			options.Filters.Add<InjectUserIdFromTokenFilter>();
		});

		return services;
	}

	private static IServiceCollection RegisterAuthenticationServices(IServiceCollection services, IConfiguration configuration)
	{
		// ASP.NET Core Identity for user management
		services.AddIdentity<User, IdentityRole>(options =>
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
		var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

		// Support base64-prefixed secrets: "base64:..."
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
		{
			throw new InvalidOperationException($"JwtSettings.Secret is too short for HS256. Provided {keyBytes.Length * 8} bits; required >= 256 bits. Use a 32+ byte (e.g., 64+ char) random secret or a base64 string prefixed with 'base64:'.");
		}

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
						var sub = claims.FirstOrDefault(c => c.Type == "sub" || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

						if (tokenType != "access" || string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(sub))
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
						if (!exists)
						{
							context.Fail("Token revoked or expired");
						}

						return Task.CompletedTask;
					}
				};
			});

		// Redis registration (singleton connection)
		var redisConnString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
		services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnString));

		// Prefer Redis-backed token store; fallback to in-memory if Redis is not reachable at runtime
		services.AddScoped<ITokenStoreService>(sp =>
		{
			var mux = sp.GetService<IConnectionMultiplexer>();
			try
			{
				if (mux is { IsConnected: true })
				{
					return new RedisTokenStoreService(mux);
				}
			}
			catch { /* ignore and fallback */ }
			return new InMemoryTokenStoreService();
		});

		return services;
	}

	private static IServiceCollection RegisterApiDocumentationServices(IServiceCollection services)
	{
		// Swagger/OpenAPI for API documentation and testing
		services.AddOpenApi();
		services.AddSwaggerGen(options =>
		{
			// JWT Bearer token authentication setup
			options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
			{
				Name = "Authorization",
				Type = SecuritySchemeType.Http,
				Scheme = "Bearer",
				BearerFormat = "JWT",
				In = ParameterLocation.Header,
				Description = "Enter your JWT token in the format: Bearer {your-token}"
			});

			// Add global security requirement so Authorize shows lock icon
			options.AddSecurityRequirement(new OpenApiSecurityRequirement
			{
				{
					new OpenApiSecurityScheme
					{
						Reference = new OpenApiReference
						{
							Type = ReferenceType.SecurityScheme,
							Id = "Bearer"
						}
					},
					new List<string>()
				}
			});

			// Filters for enhanced API documentation
			options.OperationFilter<AuthorizeCheckOperationFilter>();
			options.SchemaFilter<EnumSchemaFilter>();
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
		// Serilog logging configuration
		var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
		Directory.CreateDirectory(logsPath);

		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
			.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
			.Enrich.FromLogContext()
			.WriteTo.Console(
				outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
			.WriteTo.File(
				Path.Combine(logsPath, ".log"),
				rollingInterval: RollingInterval.Day,
				outputTemplate:
				"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
			.CreateLogger();

		services.AddLogging(loggingBuilder =>
		{
			loggingBuilder.ClearProviders();
			loggingBuilder.AddSerilog();
		});

		return services;
	}
}
