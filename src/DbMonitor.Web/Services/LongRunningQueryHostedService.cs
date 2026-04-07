using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbMonitor.Web.Services;

public class LongRunningQueryHostedService : MonitoringHostedService
{
    private readonly ILongRunningQueryMonitor _monitor;
    private readonly IActionExecutor _executor;
    private readonly LongRunningQueryOptions _options;
    private readonly MonitoringOptions _monOptions;

    public LongRunningQueryHostedService(
        ILongRunningQueryMonitor monitor,
        IActionExecutor executor,
        IOptions<LongRunningQueryOptions> options,
        IOptions<MonitoringOptions> monOptions,
        ILogger<LongRunningQueryHostedService> logger)
        : base("LongRunningQueryMonitor", logger)
    {
        _monitor = monitor;
        _executor = executor;
        _options = options.Value;
        _monOptions = monOptions.Value;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var queries = await _monitor.GetLongRunningQueriesAsync(ct);

        Logger.LogInformation("Long-running queries: {Count} detected", queries.Count);

        if (_options.Enabled && _options.KillEnabled)
        {
            foreach (var query in queries.Where(q => q.WouldBeKilled))
            {
                await _executor.KillQueryAsync(query, ActionTrigger.Automatic, false, ct);
            }
        }
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(_monOptions.LongRunningQueryIntervalSeconds), ct);
}
