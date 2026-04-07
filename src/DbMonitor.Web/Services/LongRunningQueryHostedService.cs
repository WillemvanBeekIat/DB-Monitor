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
    private readonly IOptionsMonitor<LongRunningQueryOptions> _options;
    private readonly IOptionsMonitor<MonitoringOptions> _monOptions;

    public LongRunningQueryHostedService(
        ILongRunningQueryMonitor monitor,
        IActionExecutor executor,
        IOptionsMonitor<LongRunningQueryOptions> options,
        IOptionsMonitor<MonitoringOptions> monOptions,
        ILogger<LongRunningQueryHostedService> logger)
        : base("LongRunningQueryMonitor", logger)
    {
        _monitor = monitor;
        _executor = executor;
        _options = options;
        _monOptions = monOptions;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var queries = await _monitor.GetLongRunningQueriesAsync(ct);

        var opts = _options.CurrentValue;
        Logger.LogInformation("Long-running queries: {Count} detected", queries.Count);

        if (opts.Enabled && opts.KillEnabled)
        {
            foreach (var query in queries.Where(q => q.WouldBeKilled))
            {
                await _executor.KillQueryAsync(query, ActionTrigger.Automatic, false, ct);
            }
        }
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(_monOptions.CurrentValue.LongRunningQueryIntervalSeconds), ct);
}
