using Application.Jobs;
using Domain.Constants;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

namespace Presentation.Extensions;

public static class HangfireExtensions
{
	public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
	{
		var connectionString = configuration.GetConnectionString("DefaultConnection");
		const string defaultSchemaName = "hangfire";

		services.AddHangfire(config =>
		{
			config.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString),
				new PostgreSqlStorageOptions
				{
					SchemaName = defaultSchemaName
				});

			// Global retry policy for background jobs
			GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
			{
				Attempts = 3,
				DelaysInSeconds = [30, 120, 300], // 30s, 2m, 5m
				OnAttemptsExceeded = AttemptsExceededAction.Delete
			});
		});

		// Configure server workers and named queues for workload isolation
		services.AddHangfireServer(options =>
		{
			options.Queues =
			[
				HangfireConstants.DefaultQueue
			];
		});

		// Register job wrappers
		services.AddTransient<ExampleJobs>();

		return services;
	}

	public static IApplicationBuilder UseHangfireServices(this IApplicationBuilder app, IConfiguration configuration)
	{
		var dashboardOptions = new DashboardOptions
		{
			Authorization = [new DevelopmentAuthorizationFilter()]
		};
		app.UseHangfireDashboard("/hangfire", dashboardOptions);
		return app;
	}

	public static void RegisterRecurringJobs(this IServiceProvider serviceProvider)
	{
		var recurringJobManager = serviceProvider.GetRequiredService<IRecurringJobManager>();

		// Use UTC explicitly to avoid server-local time ambiguity
		var utcTz = TimeZoneInfo.Utc;

		recurringJobManager.AddOrUpdate<ExampleJobs>(
			"example-job-1",
			x => ExampleJobs.Job1(),
			Cron.Minutely,
			new RecurringJobOptions
			{
				TimeZone = utcTz,
				MisfireHandling = MisfireHandlingMode.Relaxed
			});
	}

	// Development authorization filter that allows all access
	private class DevelopmentAuthorizationFilter : IDashboardAuthorizationFilter
	{
		public bool Authorize(DashboardContext context)
		{
			return true;
		}
	}
}