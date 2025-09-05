using Domain.Constants;
using Hangfire;

namespace Application.Jobs;

public class ExampleJobs
{
	[Queue(HangfireConstants.DefaultQueue)]
	[DisableConcurrentExecution(600)]
	public static async Task Job1()
	{
		// Inject a service and call its method here
		await Task.CompletedTask;
	}
}