using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using DbMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Infrastructure.Persistence;

public class EfAuditWriter : IAuditWriter
{
    private readonly IDbContextFactory<DbMonitorDbContext> _contextFactory;
    private readonly ILogger<EfAuditWriter> _logger;

    public EfAuditWriter(IDbContextFactory<DbMonitorDbContext> contextFactory, ILogger<EfAuditWriter> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var entity = AuditEntryEntity.FromModel(entry);
            context.AuditEntries.Add(entity);
            await context.SaveChangesAsync(ct);

            _logger.LogDebug("Wrote audit entry {Id} for {ActionType} on {Target}",
                entry.Id, entry.ActionType, entry.Target);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit entry to database");
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int count = 50, CancellationToken ct = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var entities = await context.AuditEntries
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToListAsync(ct);

            // Return in chronological order (oldest first)
            entities.Reverse();
            return entities.Select(e => e.ToModel()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read audit entries from database");
            return Array.Empty<AuditEntry>();
        }
    }
}
