using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using DbMonitor.Core.Services;

namespace DbMonitor.SqlServer.Monitors;

public class FragmentationMonitor : IFragmentationMonitor
{
    private readonly SqlServerOptions _sqlOptions;
    private readonly IOptionsMonitor<FragmentationOptions> _fragOptions;
    private readonly FragmentationEligibilityEvaluator _evaluator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FragmentationMonitor> _logger;

    public FragmentationMonitor(
        IOptions<SqlServerOptions> sqlOptions,
        IOptionsMonitor<FragmentationOptions> fragOptions,
        FragmentationEligibilityEvaluator evaluator,
        IServiceScopeFactory scopeFactory,
        ILogger<FragmentationMonitor> logger)
    {
        _sqlOptions = sqlOptions.Value;
        _fragOptions = fragOptions;
        _evaluator = evaluator;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IndexInfo>> CheckAsync(CancellationToken ct = default)
    {
        var result = new List<IndexInfo>();
        var fragOptions = _fragOptions.CurrentValue;

        if (!fragOptions.Enabled || fragOptions.MonitoredIndexes.Count == 0)
            return result;

        try
        {
            var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
            {
                ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
            };

            using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            foreach (var indexConfig in fragOptions.MonitoredIndexes)
            {
                if (!indexConfig.Enabled) continue;

                try
                {
                    var info = await CheckSingleIndexAsync(conn, indexConfig, ct);
                    if (info != null)
                    {
                        var cooldownKey = $"frag:{indexConfig.Database}.{indexConfig.Schema}.{indexConfig.Table}.{indexConfig.Index}";
                        using var scope = _scopeFactory.CreateScope();
                        var stateStore = scope.ServiceProvider.GetRequiredService<IStateStore>();
                        var cooldownUntil = await stateStore.LoadCooldownAsync(cooldownKey, ct);
                        var (eligible, reason) = _evaluator.Evaluate(info, indexConfig, cooldownUntil, fragOptions.CooldownMinutes);
                        info.IsEligible = eligible;
                        info.IneligibilityReason = reason;
                        result.Add(info);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check index {Database}.{Schema}.{Table}.{Index}",
                        indexConfig.Database, indexConfig.Schema, indexConfig.Table, indexConfig.Index);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fragmentation check failed");
        }

        return result;
    }

    private async Task<IndexInfo?> CheckSingleIndexAsync(SqlConnection conn, MonitoredIndexConfig config, CancellationToken ct)
    {
        const string fragSql = @"
SELECT
    ps.avg_fragmentation_in_percent,
    ps.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(@dbName), OBJECT_ID(@objectName), NULL, NULL, 'LIMITED') ps
INNER JOIN sys.indexes i ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.name = @indexName
  AND ps.index_level = 0";

        const string usageSql = @"
SELECT
    ISNULL(ius.user_seeks, 0),
    ISNULL(ius.user_scans, 0),
    ISNULL(ius.user_lookups, 0),
    ISNULL(ius.user_updates, 0)
FROM sys.indexes i
INNER JOIN sys.objects o ON o.object_id = i.object_id
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.dm_db_index_usage_stats ius ON ius.object_id = i.object_id
    AND ius.index_id = i.index_id
    AND ius.database_id = DB_ID(@dbName)
WHERE s.name = @schemaName
  AND o.name = @tableName
  AND i.name = @indexName";

        try
        {
            conn.ChangeDatabase(config.Database);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot switch to database {Database}", config.Database);
            return null;
        }

        double fragPercent = 0;
        long pageCount = 0;

        using (var cmd = new SqlCommand(fragSql, conn))
        {
            cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("@dbName", config.Database);
            cmd.Parameters.AddWithValue("@objectName", $"{config.Schema}.{config.Table}");
            cmd.Parameters.AddWithValue("@indexName", config.Index);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                fragPercent = reader.IsDBNull(0) ? 0 : reader.GetDouble(0);
                pageCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            }
        }

        long seeks = 0, scans = 0, lookups = 0, updates = 0;

        using (var cmd = new SqlCommand(usageSql, conn))
        {
            cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("@dbName", config.Database);
            cmd.Parameters.AddWithValue("@schemaName", config.Schema);
            cmd.Parameters.AddWithValue("@tableName", config.Table);
            cmd.Parameters.AddWithValue("@indexName", config.Index);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                seeks = reader.GetInt64(0);
                scans = reader.GetInt64(1);
                lookups = reader.GetInt64(2);
                updates = reader.GetInt64(3);
            }
        }

        var usageScore = _evaluator.CalculateUsageScore(seeks, scans, lookups, updates, config.UsageWeights);

        try { conn.ChangeDatabase("master"); } catch { /* ignore */ }

        return new IndexInfo
        {
            Database = config.Database,
            Schema = config.Schema,
            Table = config.Table,
            IndexName = config.Index,
            FragmentationPercent = fragPercent,
            PageCount = pageCount,
            UsageScore = usageScore,
            LastChecked = DateTimeOffset.UtcNow
        };
    }
}
