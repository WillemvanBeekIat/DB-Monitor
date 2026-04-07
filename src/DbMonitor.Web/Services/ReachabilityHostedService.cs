using System;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbMonitor.Web.Services;

public class ReachabilityHostedService : MonitoringHostedService
{
    private readonly IReachabilityMonitor _monitor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MonitoringOptions> _options;

    public ReachabilityHostedService(
        IReachabilityMonitor monitor,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MonitoringOptions> options,
        ILogger<ReachabilityHostedService> logger)
        : base("ReachabilityMonitor", logger)
    {
        _monitor = monitor;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var health = await _monitor.CheckAsync(ct);
        using (var scope = _scopeFactory.CreateScope())
        {
            var stateStore = scope.ServiceProvider.GetRequiredService<IStateStore>();
            await stateStore.SaveInstanceHealthAsync(health, ct);
        }
        Logger.LogInformation("Instance reachable: {Reachable}, Status: {Status}", health.IsReachable, health.Status);
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(_options.CurrentValue.InstanceProbeIntervalSeconds), ct);
}
