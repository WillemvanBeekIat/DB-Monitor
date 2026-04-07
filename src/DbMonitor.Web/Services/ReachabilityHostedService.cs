using System;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbMonitor.Web.Services;

public class ReachabilityHostedService : MonitoringHostedService
{
    private readonly IReachabilityMonitor _monitor;
    private readonly IStateStore _stateStore;
    private readonly MonitoringOptions _options;

    public ReachabilityHostedService(
        IReachabilityMonitor monitor,
        IStateStore stateStore,
        IOptions<MonitoringOptions> options,
        ILogger<ReachabilityHostedService> logger)
        : base("ReachabilityMonitor", logger)
    {
        _monitor = monitor;
        _stateStore = stateStore;
        _options = options.Value;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var health = await _monitor.CheckAsync(ct);
        await _stateStore.SaveInstanceHealthAsync(health, ct);
        Logger.LogInformation("Instance reachable: {Reachable}, Status: {Status}", health.IsReachable, health.Status);
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(_options.InstanceProbeIntervalSeconds), ct);
}
