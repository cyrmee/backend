using System.Reflection;
using Domain.Constants;
using Domain.Entities.Base;
using Domain.Settings;
using Hangfire;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Jobs;

public class SoftDeleteCleanupJobs(
    IServiceScopeFactory scopeFactory,
    ILogger<SoftDeleteCleanupJobs> logger,
    IOptions<SoftDeleteSettings> softDeleteSettings)
{
    private static List<Type>? _softDeletableTypes;
    private static readonly Lock TypesLock = new();

    private static readonly MethodInfo PurgeGenericMethod = typeof(SoftDeleteCleanupJobs)
        .GetMethod(nameof(PurgeTypeAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    [Queue(HangfireQueues.SoftDeleteCleanupQueue)]
    [DisableConcurrentExecution(300)]
    public async Task PurgeOldSoftDeletedAsync()
    {
        var started = DateTime.UtcNow;
        var settings = softDeleteSettings.Value;
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var threshold = started.AddDays(-settings.RetentionDays);
        var batchSize = settings.BatchSize;

        // Cache entity types once
        var types = _softDeletableTypes;
        if (types == null)
            lock (TypesLock)
            {
                types = _softDeletableTypes;
                if (types == null)
                {
                    var discovered = context.Model.GetEntityTypes()
                        .Select(t => t.ClrType)
                        .Where(t => typeof(ISoftDeletableEntity).IsAssignableFrom(t) && !t.IsAbstract)
                        .ToList();
                    _softDeletableTypes = discovered;
                    types = discovered;
                }
            }

        if (types.Count == 0)
        {
            logger.LogInformation("Soft delete purge found no soft deletable entity types configured");
            return;
        }

        var totalDeleted = 0;
        foreach (var type in types)
            try
            {
                var task = (Task<int>)PurgeGenericMethod.MakeGenericMethod(type)
                    .Invoke(null, [context, threshold, batchSize, CancellationToken.None])!;
                var deleted = await task;
                totalDeleted += deleted;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error purging soft deleted entities for type {Type}", type.Name);
            }

        logger.LogInformation(
            "Soft delete purge removed {Count} entities older than {RetentionDays} days in {ElapsedMs} ms across {TypeCount} types (batchSize={BatchSize})",
            totalDeleted, settings.RetentionDays, (DateTime.UtcNow - started).TotalMilliseconds, types.Count, batchSize);
    }

    // Generic batch purge for a single entity type using ExecuteDelete to avoid materializing entities
    private static async Task<int> PurgeTypeAsync<TEntity>(ApplicationDbContext context, DateTime threshold,
        int batchSize, CancellationToken cancellationToken)
        where TEntity : class, ISoftDeletableEntity
    {
        var total = 0;
        while (true)
        {
            // Note: OrderBy ensures deterministic batching if many rows share same DeletedAt
            var deleted = await context.Set<TEntity>()
                .IgnoreQueryFilters()
                .Where(e => e.IsDeleted && e.DeletedAt <= threshold)
                .OrderBy(e => e.DeletedAt)
                .Take(batchSize)
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0) break;
            total += deleted;
        }

        return total;
    }
}