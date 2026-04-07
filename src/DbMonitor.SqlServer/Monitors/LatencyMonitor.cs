using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using DbMonitor.Core.Services;

namespace DbMonitor.SqlServer.Monitors;

public class LatencyMonitor : ILatencyMonitor
{
    private readonly SqlServerOptions _sqlOptions;
    private readonly LatencyOptions _latencyOptions;
    private readonly HealthEvaluator _evaluator;
    private readonly ILogger<LatencyMonitor> _logger;
    private readonly List<LatencySnapshot> _history = new();
    private readonly object _lock = new();

    public LatencyMonitor(
        IOptions<SqlServerOptions> sqlOptions,
        IOptions<LatencyOptions> latencyOptions,
        HealthEvaluator evaluator,
        ILogger<LatencyMonitor> logger)
    {
        _sqlOptions = sqlOptions.Value;
        _latencyOptions = latencyOptions.Value;
        _evaluator = evaluator;
        _logger = logger;
    }

    public async Task<LatencySnapshot> MeasureAsync(CancellationToken ct = default)
    {
        var snapshot = new LatencySnapshot { Timestamp = DateTimeOffset.UtcNow };
        var sw = Stopwatch.StartNew();

        try
        {
            var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
            {
                ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
            };

            using var conn = new SqlConnection(csb.ConnectionString);
            sw.Restart();
            await conn.OpenAsync(ct);
            snapshot.ConnectionOpenMs = sw.Elapsed.TotalMilliseconds;

            using var cmd = new SqlCommand("SELECT @@VERSION", conn);
            cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
            sw.Restart();
            await cmd.ExecuteScalarAsync(ct);
            snapshot.ProbeQueryMs = sw.Elapsed.TotalMilliseconds;

            using var metaCmd = new SqlCommand("SELECT COUNT(*) FROM sys.databases", conn);
            metaCmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
            sw.Restart();
            await metaCmd.ExecuteScalarAsync(ct);
            snapshot.MetadataQueryMs = sw.Elapsed.TotalMilliseconds;

            snapshot.Status = _evaluator.EvaluateLatency(snapshot.ProbeQueryMs);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            snapshot.Status = HealthStatus.Critical;
            _logger.LogWarning(ex, "Latency measurement failed");
        }

        lock (_lock)
        {
            _history.Add(snapshot);
            if (_history.Count > _latencyOptions.MovingAverageWindow * 2)
                _history.RemoveAt(0);
        }

        return snapshot;
    }

    public IReadOnlyList<LatencySnapshot> GetHistory()
    {
        lock (_lock)
            return _history.ToArray();
    }
}
