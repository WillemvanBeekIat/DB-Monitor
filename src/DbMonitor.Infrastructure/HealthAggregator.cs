using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using Microsoft.Extensions.Options;

namespace DbMonitor.Infrastructure;

public class HealthAggregator : IHealthAggregator
{
    private readonly IReachabilityMonitor _reachability;
    private readonly ILatencyMonitor _latency;
    private readonly IFragmentationMonitor _fragmentation;
    private readonly ILongRunningQueryMonitor _queryMonitor;
    private readonly IErrorLogMonitor _errorLog;
    private readonly IAuditWriter _auditWriter;
    private readonly IOptions<FragmentationOptions> _fragOptions;
    private DashboardSummary? _lastSummary;

    public DashboardSummary? LastSummary => _lastSummary;

    public HealthAggregator(
        IReachabilityMonitor reachability,
        ILatencyMonitor latency,
        IFragmentationMonitor fragmentation,
        ILongRunningQueryMonitor queryMonitor,
        IErrorLogMonitor errorLog,
        IAuditWriter auditWriter,
        IOptions<FragmentationOptions> fragOptions)
    {
        _reachability = reachability;
        _latency = latency;
        _fragmentation = fragmentation;
        _queryMonitor = queryMonitor;
        _errorLog = errorLog;
        _auditWriter = auditWriter;
        _fragOptions = fragOptions;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var instanceHealth = await _reachability.CheckAsync(ct);
        var latencyHistory = _latency.GetHistory();
        var indexes = await _fragmentation.CheckAsync(ct);
        var queries = await _queryMonitor.GetLongRunningQueriesAsync(ct);
        var errors = await _errorLog.ReadNewErrorsAsync(ct);
        var recentActions = await _auditWriter.GetRecentAsync(10, ct);

        var overallStatus = DetermineOverallStatus(instanceHealth, latencyHistory, queries.Count, errors.Count);

        var summary = new DashboardSummary
        {
            Timestamp = DateTimeOffset.UtcNow,
            OverallStatus = overallStatus,
            InstanceHealth = instanceHealth,
            LongRunningQueryCount = queries.Count,
            ActionableFragmentedIndexCount = indexes.Count(i => i.IsEligible),
            InternalErrorCount = errors.Count,
            RecentActions = recentActions.ToList(),
            IsDryRunEnabled = _fragOptions.Value.DryRun,
            RecentLatency = latencyHistory.TakeLast(20).ToList()
        };

        _lastSummary = summary;
        return summary;
    }

    private static HealthStatus DetermineOverallStatus(
        InstanceHealth instance,
        IReadOnlyList<LatencySnapshot> latency,
        int longRunningQueries,
        int errors)
    {
        if (!instance.IsReachable) return HealthStatus.Critical;

        if (errors > 0) return HealthStatus.Warning;
        if (longRunningQueries > 5) return HealthStatus.Warning;

        if (latency.Count > 0)
        {
            var worst = latency.Max(l => (int)l.Status);
            if (worst >= (int)HealthStatus.Critical) return HealthStatus.Critical;
            if (worst >= (int)HealthStatus.Warning) return HealthStatus.Warning;
        }

        return HealthStatus.Healthy;
    }
}
