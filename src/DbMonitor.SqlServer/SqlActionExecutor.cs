using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;

namespace DbMonitor.SqlServer;

public class SqlActionExecutor : IActionExecutor
{
    private readonly SqlServerOptions _sqlOptions;
    private readonly FragmentationOptions _fragOptions;
    private readonly LongRunningQueryOptions _queryOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SqlActionExecutor> _logger;

    public SqlActionExecutor(
        IOptions<SqlServerOptions> sqlOptions,
        IOptions<FragmentationOptions> fragOptions,
        IOptions<LongRunningQueryOptions> queryOptions,
        IServiceScopeFactory scopeFactory,
        ILogger<SqlActionExecutor> logger)
    {
        _sqlOptions = sqlOptions.Value;
        _fragOptions = fragOptions.Value;
        _queryOptions = queryOptions.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<AuditEntry> ReorganizeIndexAsync(IndexInfo index, ActionTrigger trigger, bool manualOverride = false, CancellationToken ct = default)
    {
        var entry = new AuditEntry
        {
            ActionType = ActionType.ReorganizeIndex,
            Trigger = trigger,
            Target = $"{index.Database}.{index.Schema}.{index.Table}.{index.IndexName}",
            IsManualOverride = manualOverride,
            TriggeredBy = trigger == ActionTrigger.Manual ? "UI" : "AutoReorganize"
        };

        entry.IsDryRun = _fragOptions.DryRun;

        try
        {
            if (_fragOptions.DryRun)
            {
                entry.Reason = $"DryRun: would reorganize index [{index.IndexName}] on [{index.Table}] (fragmentation: {index.FragmentationPercent:F1}%)";
                entry.Outcome = ActionOutcome.DryRun;
                _logger.LogInformation("[DRY-RUN] Would reorganize {Target}", entry.Target);
            }
            else
            {
                var sql = $"ALTER INDEX [{index.IndexName}] ON [{index.Database}].[{index.Schema}].[{index.Table}] REORGANIZE";
                var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
                {
                    ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
                };

                using var conn = new SqlConnection(csb.ConnectionString);
                await conn.OpenAsync(ct);
                conn.ChangeDatabase(index.Database);

                using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds * 10;
                await cmd.ExecuteNonQueryAsync(ct);

                entry.Reason = $"Reorganized index [{index.IndexName}] on [{index.Table}] (fragmentation was {index.FragmentationPercent:F1}%)";
                entry.Outcome = ActionOutcome.Executed;

                var cooldownKey = $"frag:{index.Database}.{index.Schema}.{index.Table}.{index.IndexName}";
                using (var scope = _scopeFactory.CreateScope())
                {
                    var stateStore = scope.ServiceProvider.GetRequiredService<IStateStore>();
                    await stateStore.SaveCooldownAsync(cooldownKey,
                        DateTimeOffset.UtcNow.AddMinutes(_fragOptions.CooldownMinutes), ct);
                }

                _logger.LogInformation("Reorganized index {Target}", entry.Target);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            entry.Outcome = ActionOutcome.Failed;
            entry.ErrorMessage = ex.Message;
            entry.Reason = $"Failed to reorganize: {ex.Message}";
            _logger.LogError(ex, "Failed to reorganize index {Target}", entry.Target);
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
            await auditWriter.WriteAsync(entry, ct);
        }
        return entry;
    }

    public async Task<AuditEntry> KillQueryAsync(LongRunningQuery query, ActionTrigger trigger, bool manualOverride = false, CancellationToken ct = default)
    {
        var entry = new AuditEntry
        {
            ActionType = ActionType.KillQuery,
            Trigger = trigger,
            Target = $"Session {query.SessionId} ({query.LoginName}@{query.HostName})",
            IsManualOverride = manualOverride,
            TriggeredBy = trigger == ActionTrigger.Manual ? "UI" : "AutoKill"
        };

        entry.IsDryRun = _queryOptions.DryRun;

        try
        {
            if (_queryOptions.DryRun)
            {
                entry.Reason = $"DryRun: would kill session {query.SessionId} (elapsed: {query.ElapsedDuration.TotalSeconds:F0}s)";
                entry.Outcome = ActionOutcome.DryRun;
                _logger.LogInformation("[DRY-RUN] Would kill session {SessionId}", query.SessionId);
            }
            else
            {
                var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
                {
                    ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
                };

                using var conn = new SqlConnection(csb.ConnectionString);
                await conn.OpenAsync(ct);

                using var cmd = new SqlCommand($"KILL {query.SessionId}", conn);
                cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
                await cmd.ExecuteNonQueryAsync(ct);

                entry.Reason = $"Killed session {query.SessionId} - {query.LoginName}@{query.HostName} elapsed {query.ElapsedDuration.TotalSeconds:F0}s{(manualOverride ? " (manual override)" : "")}";
                entry.Outcome = ActionOutcome.Executed;
                _logger.LogInformation("Killed session {SessionId}", query.SessionId);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            entry.Outcome = ActionOutcome.Failed;
            entry.ErrorMessage = ex.Message;
            entry.Reason = $"Failed to kill session {query.SessionId}: {ex.Message}";
            _logger.LogError(ex, "Failed to kill session {SessionId}", query.SessionId);
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
            await auditWriter.WriteAsync(entry, ct);
        }
        return entry;
    }
}
