using Domain.Entities.Base;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Interceptors;

public class ChangeTrackingInterceptor(
	ICurrentUserService? currentUserService,
	IHttpContextAccessor? httpContextAccessor,
	ILogger<ChangeTrackingInterceptor>? logger) : SaveChangesInterceptor
{
	public ChangeTrackingInterceptor() : this(null, null, null)
	{
	}

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		UpdateEntities(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
		InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		UpdateEntities(eventData.Context);
		return await base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private void UpdateEntities(DbContext? context)
	{
		if (context == null) return;

		var now = DateTime.UtcNow;
		string? email = null;
		if (currentUserService != null && httpContextAccessor != null)
		{
			var token = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
			if (!string.IsNullOrWhiteSpace(token))
				try
				{
					var authResponse = currentUserService.GetCurrentUser(token);
					email = authResponse.Email;
				}
				catch (Exception ex)
				{
					logger?.LogDebug(ex, "Failed to resolve current user from token for auditing.");
				}
		}

		foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
			switch (entry.State)
			{
				case EntityState.Added:
					if (entry.Entity.Id == Guid.Empty)
						entry.Entity.Id = Guid.NewGuid();
					entry.Entity.CreatedAt = now;
					entry.Entity.UpdatedAt = now;
					entry.Entity.CreatedBy = email;
					entry.Entity.UpdatedBy = email;
					break;
				case EntityState.Modified:
					entry.Property(p => p.CreatedAt).IsModified = false;
					entry.Property(p => p.CreatedBy).IsModified = false;
					entry.Entity.UpdatedAt = now;
					entry.Entity.UpdatedBy = email;
					break;
				case EntityState.Deleted when entry.Entity is ISoftDeletableEntity softDeletable:
					entry.State = EntityState.Modified;
					softDeletable.IsDeleted = true;
					softDeletable.DeletedAt = now;
					softDeletable.DeletedBy = email;
					break;
				case EntityState.Detached:
				case EntityState.Unchanged:
				case EntityState.Deleted:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
	}
}