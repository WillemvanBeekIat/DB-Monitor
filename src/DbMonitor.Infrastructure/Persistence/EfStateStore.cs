using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using DbMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Infrastructure.Persistence;

public class EfStateStore : IStateStore
{
    private readonly IDbContextFactory<DbMonitorDbContext> _contextFactory;
    private readonly ILogger<EfStateStore> _logger;

    public EfStateStore(IDbContextFactory<DbMonitorDbContext> contextFactory, ILogger<EfStateStore> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task SaveInstanceHealthAsync(InstanceHealth health, CancellationToken ct = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Mark all existing records as not current
            await context.InstanceHealthSnapshots
                .Where(h => h.IsCurrent)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.IsCurrent, false), ct);

            // Add new current record
            var entity = InstanceHealthEntity.FromModel(health);
            entity.IsCurrent = true;
            context.InstanceHealthSnapshots.Add(entity);
            await context.SaveChangesAsync(ct);

            _logger.LogDebug("Saved instance health snapshot to database");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save instance health to database");
        }
    }

    public async Task<InstanceHealth?> LoadInstanceHealthAsync(CancellationToken ct = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var entity = await context.InstanceHealthSnapshots
                .Include(h => h.Databases)
                .Where(h => h.IsCurrent)
                .OrderByDescending(h => h.Timestamp)
                .FirstOrDefaultAsync(ct);

            return entity?.ToModel();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load instance health from database");
            return null;
        }
    }

    public async Task SaveCooldownAsync(string key, DateTimeOffset until, CancellationToken ct = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var existing = await context.Cooldowns.FindAsync(new object[] { key }, ct);
            if (existing != null)
            {
                existing.Until = until;
            }
            else
            {
                context.Cooldowns.Add(new CooldownEntity { Key = key, Until = until });
            }

            await context.SaveChangesAsync(ct);
            _logger.LogDebug("Saved cooldown {Key} until {Until}", key, until);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save cooldown {Key} to database", key);
        }
    }

    public async Task<DateTimeOffset?> LoadCooldownAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var entity = await context.Cooldowns.FindAsync(new object[] { key }, ct);
            return entity?.Until;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cooldown {Key} from database", key);
            return null;
        }
    }

    public async Task SaveErrorBookmarkAsync(string bookmark, CancellationToken ct = default)
    {
        const string key = "error-log-bookmark";
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var existing = await context.Bookmarks.FindAsync(new object[] { key }, ct);
            if (existing != null)
            {
                existing.Value = bookmark;
            }
            else
            {
                context.Bookmarks.Add(new BookmarkEntity { Key = key, Value = bookmark });
            }

            await context.SaveChangesAsync(ct);
            _logger.LogDebug("Saved error bookmark to database");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save error bookmark to database");
        }
    }

    public async Task<string?> LoadErrorBookmarkAsync(CancellationToken ct = default)
    {
        const string key = "error-log-bookmark";
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var entity = await context.Bookmarks.FindAsync(new object[] { key }, ct);
            return entity?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load error bookmark from database");
            return null;
        }
    }
}
