using System.Reflection;
using Domain.Models;
using Infrastructure.Interceptors;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class ApplicationDbContext(
	DbContextOptions<ApplicationDbContext> options,
	AuditableEntityInterceptor auditableEntityInterceptor
) : IdentityDbContext<User>(options)
{
	public DbSet<AppSettings> AppSettings { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		base.OnConfiguring(optionsBuilder);
		optionsBuilder.AddInterceptors(auditableEntityInterceptor);
	}

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
	}
}