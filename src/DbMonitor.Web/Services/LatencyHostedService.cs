using System;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbMonitor.Web.Services;

public class LatencyHostedService : MonitoringHostedService
{
    private readonly ILatencyMonitor _monitor;
    private readonly MonitoringOptions _options;

    public LatencyHostedService(
        ILatencyMonitor monitor,
        IOptions<MonitoringOptions> options,
        ILogger<LatencyHostedService> logger)
        : base("LatencyMonitor", logger)
    {
        _monitor = monitor;
        _options = options.Value;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var snapshot = await _monitor.MeasureAsync(ct);
        Logger.LogInformation("Latency: connection={ConnMs:F0}ms probe={ProbeMs:F0}ms status={Status}",
            snapshot.ConnectionOpenMs, snapshot.ProbeQueryMs, snapshot.Status);
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(_options.LatencyIntervalSeconds), ct);
}
