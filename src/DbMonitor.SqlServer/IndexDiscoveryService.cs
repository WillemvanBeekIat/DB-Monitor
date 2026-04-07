using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;

namespace DbMonitor.SqlServer;

public class IndexDiscoveryService : IIndexDiscoveryService
{
    private readonly SqlServerOptions _sqlOptions;
    private readonly IOptionsMonitor<FragmentationOptions> _fragOptions;
    private readonly ILogger<IndexDiscoveryService> _logger;

    public IndexDiscoveryService(
        IOptions<SqlServerOptions> sqlOptions,
        IOptionsMonitor<FragmentationOptions> fragOptions,
        ILogger<IndexDiscoveryService> logger)
    {
        _sqlOptions = sqlOptions.Value;
        _fragOptions = fragOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoverableIndex>> DiscoverAsync(CancellationToken ct = default)
    {
        var result = new List<DiscoverableIndex>();

        if (string.IsNullOrEmpty(_sqlOptions.ConnectionString))
            return result;

        // Build a set of already-monitored index keys for the AlreadyMonitored flag
        var monitoredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in _fragOptions.CurrentValue.MonitoredIndexes)
            monitoredKeys.Add($"{m.Database}.{m.Schema}.{m.Table}.{m.Index}");

        try
        {
            var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
            {
                ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
            };

            using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            var databases = await GetDatabasesAsync(conn, ct);

            foreach (var db in databases)
            {
                try
                {
                    conn.ChangeDatabase(db);
                    var indexes = await GetIndexesForDatabaseAsync(conn, db, monitoredKeys, ct);
                    result.AddRange(indexes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not discover indexes in database {Database}", db);
                }
            }

            try { conn.ChangeDatabase("master"); } catch { /* ignore */ }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index discovery failed");
        }

        return result;
    }

    private async Task<List<string>> GetDatabasesAsync(SqlConnection conn, CancellationToken ct)
    {
        const string sql = @"
SELECT name
FROM sys.databases
WHERE state_desc = 'ONLINE'
  AND name NOT IN ('master','tempdb','model','msdb')
ORDER BY name";

        var databases = new List<string>();

        // Apply inclusion/exclusion filters from config
        var included = _sqlOptions.IncludedDatabases;
        var excluded = new HashSet<string>(_sqlOptions.ExcludedDatabases, StringComparer.OrdinalIgnoreCase);

        using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            if (excluded.Contains(name)) continue;
            if (included.Count > 0 && !included.Contains(name)) continue;
            databases.Add(name);
        }

        return databases;
    }

    private async Task<List<DiscoverableIndex>> GetIndexesForDatabaseAsync(
        SqlConnection conn, string database, HashSet<string> monitoredKeys, CancellationToken ct)
    {
        // Query user indexes with their fragmentation stats
        const string sql = @"
SELECT
    s.name          AS SchemaName,
    t.name          AS TableName,
    i.name          AS IndexName,
    i.type_desc     AS IndexType,
    i.is_unique     AS IsUnique,
    i.is_primary_key AS IsPrimaryKey,
    ISNULL(ps.page_count, 0)                    AS PageCount,
    ISNULL(ps.avg_fragmentation_in_percent, 0)  AS FragmentationPercent
FROM sys.indexes i
INNER JOIN sys.tables t   ON t.object_id = i.object_id
INNER JOIN sys.schemas s  ON s.schema_id = t.schema_id
LEFT JOIN sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
    ON ps.object_id = i.object_id AND ps.index_id = i.index_id AND ps.index_level = 0
WHERE i.type > 0           -- exclude heaps
  AND t.is_ms_shipped = 0  -- exclude system tables
ORDER BY s.name, t.name, i.name";

        var indexes = new List<DiscoverableIndex>();

        using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var indexName = reader.GetString(2);
            var indexType = reader.GetString(3);
            var isUnique = reader.GetBoolean(4);
            var isPrimaryKey = reader.GetBoolean(5);
            var pageCount = reader.GetInt64(6);
            var fragPct = reader.GetDouble(7);

            var key = $"{database}.{schema}.{table}.{indexName}";

            indexes.Add(new DiscoverableIndex
            {
                Database = database,
                Schema = schema,
                Table = table,
                IndexName = indexName,
                IndexType = indexType,
                IsUnique = isUnique,
                IsPrimaryKey = isPrimaryKey,
                PageCount = pageCount,
                FragmentationPercent = fragPct,
                AlreadyMonitored = monitoredKeys.Contains(key)
            });
        }

        return indexes;
    }
}
