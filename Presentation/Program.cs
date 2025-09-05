using Infrastructure;
using Presentation.Extensions;
using Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Register all services using the extension method
builder.Services.RegisterAllServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapOpenApi();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/openapi/v1.json", "Backend API v1");
	options.RoutePrefix = "swagger"; // Access at /swagger
});

app.UseHttpsRedirection();

// Add custom middlewares
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// Add Identity middleware
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Seed the database
using (var scope = app.Services.CreateScope())
{
	var services = scope.ServiceProvider;
	try
	{
		await Seeder.SeedAsync(services);
	}
	catch (Exception ex)
	{
		var logger = services.GetRequiredService<ILogger<Program>>();
		logger.LogError(ex, "An error occurred while seeding the database.");
	}
}

app.Run();