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

namespace DbMonitor.SqlServer.Monitors;

public class UsageMonitor : IUsageMonitor
{
    private readonly SqlServerOptions _sqlOptions;
    private readonly UsageOptions _usageOptions;
    private readonly FragmentationOptions _fragOptions;
    private readonly ILogger<UsageMonitor> _logger;

    private const string UsageSql = @"
SELECT
    DB_NAME() AS database_name,
    s.name AS schema_name,
    o.name AS table_name,
    i.name AS index_name,
    ISNULL(ius.user_seeks, 0),
    ISNULL(ius.user_scans, 0),
    ISNULL(ius.user_lookups, 0),
    ISNULL(ius.user_updates, 0),
    ISNULL(ius.last_user_seek, ISNULL(ius.last_user_scan, ius.last_user_lookup)) AS last_access
FROM sys.indexes i
INNER JOIN sys.objects o ON o.object_id = i.object_id
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.dm_db_index_usage_stats ius ON ius.object_id = i.object_id
    AND ius.index_id = i.index_id
    AND ius.database_id = DB_ID()
WHERE o.type = 'U'
  AND i.type > 0
  AND s.name = @schema
  AND o.name = @table";

    public UsageMonitor(
        IOptions<SqlServerOptions> sqlOptions,
        IOptions<UsageOptions> usageOptions,
        IOptions<FragmentationOptions> fragOptions,
        ILogger<UsageMonitor> logger)
    {
        _sqlOptions = sqlOptions.Value;
        _usageOptions = usageOptions.Value;
        _fragOptions = fragOptions.Value;
        _logger = logger;
    }

    public async Task<UsageSnapshot> CaptureAsync(CancellationToken ct = default)
    {
        var snapshot = new UsageSnapshot { Timestamp = DateTimeOffset.UtcNow };

        if (!_usageOptions.Enabled || _fragOptions.MonitoredIndexes.Count == 0)
            return snapshot;

        try
        {
            var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
            {
                ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
            };

            using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            foreach (var indexConfig in _fragOptions.MonitoredIndexes)
            {
                if (!indexConfig.Enabled) continue;

                try
                {
                    conn.ChangeDatabase(indexConfig.Database);

                    using var cmd = new SqlCommand(UsageSql, conn);
                    cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
                    cmd.Parameters.AddWithValue("@schema", indexConfig.Schema);
                    cmd.Parameters.AddWithValue("@table", indexConfig.Table);

                    using var reader = await cmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var seeks = reader.GetInt64(4);
                        var scans = reader.GetInt64(5);
                        var lookups = reader.GetInt64(6);
                        var updates = reader.GetInt64(7);

                        var usage = new ObjectUsage
                        {
                            Database = reader.IsDBNull(0) ? indexConfig.Database : reader.GetString(0),
                            Schema = reader.IsDBNull(1) ? indexConfig.Schema : reader.GetString(1),
                            TableName = reader.IsDBNull(2) ? indexConfig.Table : reader.GetString(2),
                            IndexName = reader.IsDBNull(3) ? null : reader.GetString(3),
                            UserSeeks = seeks,
                            UserScans = scans,
                            UserLookups = lookups,
                            UserUpdates = updates,
                            LastAccess = reader.IsDBNull(8)
                                ? DateTimeOffset.MinValue
                                : new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero),
                            CompositeScore = (seeks * indexConfig.UsageWeights.SeekWeight)
                                           + (scans * indexConfig.UsageWeights.ScanWeight)
                                           + (lookups * indexConfig.UsageWeights.LookupWeight)
                                           + (updates * indexConfig.UsageWeights.UpdateWeight)
                        };
                        snapshot.Objects.Add(usage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to capture usage for {Database}.{Schema}.{Table}",
                        indexConfig.Database, indexConfig.Schema, indexConfig.Table);
                }
                finally
                {
                    try { conn.ChangeDatabase("master"); } catch { /* ignore */ }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Usage snapshot failed");
        }

        return snapshot;
    }
}
