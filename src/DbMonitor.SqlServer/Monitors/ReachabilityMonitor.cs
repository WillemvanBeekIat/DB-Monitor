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

public class ReachabilityMonitor : IReachabilityMonitor
{
    private readonly SqlServerOptions _options;
    private readonly ILogger<ReachabilityMonitor> _logger;
    private int _consecutiveFailures;

    public ReachabilityMonitor(IOptions<SqlServerOptions> options, ILogger<ReachabilityMonitor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InstanceHealth> CheckAsync(CancellationToken ct = default)
    {
        var health = new InstanceHealth { Timestamp = DateTimeOffset.UtcNow };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var csb = new SqlConnectionStringBuilder(_options.ConnectionString)
            {
                ConnectTimeout = _options.ConnectionTimeoutSeconds
            };

            using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);
            health.ConnectionOpenMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            using var cmd = new SqlCommand("SELECT 1", conn);
            cmd.CommandTimeout = _options.CommandTimeoutSeconds;
            await cmd.ExecuteScalarAsync(ct);
            health.ProbeQueryMs = sw.Elapsed.TotalMilliseconds;

            health.IsReachable = true;
            _consecutiveFailures = 0;

            health.Databases = await CheckDatabasesAsync(conn, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            health.IsReachable = false;
            health.ErrorMessage = ex.Message;
            health.ConsecutiveFailures = _consecutiveFailures;
            _logger.LogWarning(ex, "Instance reachability check failed (consecutive failures: {Count})", _consecutiveFailures);
        }

        health.ConsecutiveFailures = _consecutiveFailures;
        health.Status = health.IsReachable ? HealthStatus.Healthy : HealthStatus.Critical;
        return health;
    }

    private async Task<List<DatabaseHealth>> CheckDatabasesAsync(SqlConnection conn, CancellationToken ct)
    {
        var result = new List<DatabaseHealth>();

        const string sql = @"
SELECT name, state_desc
FROM sys.databases
WHERE state_desc = 'ONLINE'
  AND name NOT IN ('tempdb','model','msdb','master')";

        using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = _options.CommandTimeoutSeconds;
        using var reader = await cmd.ExecuteReaderAsync(ct);

        var allDbs = new List<(string Name, string State)>();
        while (await reader.ReadAsync(ct))
            allDbs.Add((reader.GetString(0), reader.GetString(1)));

        await reader.CloseAsync();

        foreach (var (name, state) in allDbs)
        {
            if (_options.ExcludedDatabases.Contains(name))
                continue;
            if (_options.IncludedDatabases.Count > 0 && !_options.IncludedDatabases.Contains(name))
                continue;

            var dbHealth = new DatabaseHealth
            {
                DatabaseName = name,
                IsOnline = string.Equals(state, "ONLINE", StringComparison.OrdinalIgnoreCase),
                State = state
            };

            try
            {
                conn.ChangeDatabase(name);
                using var probeCmd = new SqlCommand("SELECT 1", conn);
                probeCmd.CommandTimeout = _options.CommandTimeoutSeconds;
                await probeCmd.ExecuteScalarAsync(ct);
                conn.ChangeDatabase("master");
                dbHealth.IsReachable = true;
            }
            catch (Exception ex)
            {
                dbHealth.IsReachable = false;
                dbHealth.ErrorMessage = ex.Message;
                try { conn.ChangeDatabase("master"); } catch { /* ignore */ }
            }

            result.Add(dbHealth);
        }

        return result;
    }
}
