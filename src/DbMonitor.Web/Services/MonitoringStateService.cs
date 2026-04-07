using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Web.Services;

public class MonitoringStateService
{
    private readonly IReachabilityMonitor _reachability;
    private readonly ILatencyMonitor _latency;
    private readonly IFragmentationMonitor _fragmentation;
    private readonly ILongRunningQueryMonitor _queryMonitor;
    private readonly IErrorLogMonitor _errorLog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonitoringStateService> _logger;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DashboardSummary? _lastSummary;
    private IReadOnlyList<LongRunningQuery> _lastQueries = Array.Empty<LongRunningQuery>();
    private IReadOnlyList<IndexInfo> _lastIndexes = Array.Empty<IndexInfo>();
    private IReadOnlyList<SqlErrorEntry> _lastErrors = Array.Empty<SqlErrorEntry>();
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;

    public DashboardSummary? LastSummary => _lastSummary;
    public IReadOnlyList<LongRunningQuery> LastQueries => _lastQueries;
    public IReadOnlyList<IndexInfo> LastIndexes => _lastIndexes;
    public IReadOnlyList<SqlErrorEntry> LastErrors => _lastErrors;
    public DateTimeOffset LastRefresh => _lastRefresh;

    public event Action? OnStateChanged;

    public MonitoringStateService(
        IReachabilityMonitor reachability,
        ILatencyMonitor latency,
        IFragmentationMonitor fragmentation,
        ILongRunningQueryMonitor queryMonitor,
        IErrorLogMonitor errorLog,
        IServiceScopeFactory scopeFactory,
        ILogger<MonitoringStateService> logger)
    {
        _reachability = reachability;
        _latency = latency;
        _fragmentation = fragmentation;
        _queryMonitor = queryMonitor;
        _errorLog = errorLog;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (!await _refreshLock.WaitAsync(0, ct)) return;

        try
        {
            var instanceHealth = await _reachability.CheckAsync(ct);
            var latencyHistory = _latency.GetHistory();
            var indexes = await _fragmentation.CheckAsync(ct);
            var queries = await _queryMonitor.GetLongRunningQueriesAsync(ct);
            var errors = await _errorLog.ReadNewErrorsAsync(ct);

            IReadOnlyList<AuditEntry> recentActions;
            using (var scope = _scopeFactory.CreateScope())
            {
                var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
                recentActions = await auditWriter.GetRecentAsync(10, ct);
            }

            _lastQueries = queries;
            _lastIndexes = indexes;
            _lastErrors = errors;

            _lastSummary = new DashboardSummary
            {
                Timestamp = DateTimeOffset.UtcNow,
                OverallStatus = DetermineStatus(instanceHealth, queries.Count, errors.Count, latencyHistory),
                InstanceHealth = instanceHealth,
                LongRunningQueryCount = queries.Count,
                ActionableFragmentedIndexCount = indexes.Count(i => i.IsEligible),
                InternalErrorCount = errors.Count,
                RecentActions = recentActions.ToList(),
                RecentLatency = latencyHistory.TakeLast(20).ToList()
            };

            _lastRefresh = DateTimeOffset.UtcNow;
            OnStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "State refresh failed");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static HealthStatus DetermineStatus(
        InstanceHealth instance,
        int queryCount,
        int errorCount,
        IReadOnlyList<LatencySnapshot> latency)
    {
        if (!instance.IsReachable) return HealthStatus.Critical;
        if (errorCount > 0) return HealthStatus.Warning;
        if (queryCount > 5) return HealthStatus.Warning;
        if (latency.Count > 0 && latency[^1].Status == HealthStatus.Critical) return HealthStatus.Critical;
        if (latency.Count > 0 && latency[^1].Status == HealthStatus.Warning) return HealthStatus.Warning;
        return HealthStatus.Healthy;
    }
}
