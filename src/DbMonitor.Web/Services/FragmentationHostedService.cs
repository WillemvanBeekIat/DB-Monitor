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

public class FragmentationHostedService : MonitoringHostedService
{
    private readonly IFragmentationMonitor _monitor;
    private readonly IActionExecutor _executor;
    private readonly FragmentationOptions _options;
    private readonly MonitoringOptions _monOptions;

    public FragmentationHostedService(
        IFragmentationMonitor monitor,
        IActionExecutor executor,
        IOptions<FragmentationOptions> options,
        IOptions<MonitoringOptions> monOptions,
        ILogger<FragmentationHostedService> logger)
        : base("FragmentationMonitor", logger)
    {
        _monitor = monitor;
        _executor = executor;
        _options = options.Value;
        _monOptions = monOptions.Value;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var indexes = await _monitor.CheckAsync(ct);
        var eligible = indexes.Where(i => i.IsEligible).ToList();

        Logger.LogInformation("Fragmentation check: {Total} monitored, {Eligible} eligible",
            indexes.Count, eligible.Count);

        if (_options.Enabled && eligible.Count > 0)
        {
            var toProcess = eligible.Take(_options.MaxConcurrentActions).ToList();
            foreach (var index in toProcess)
            {
                await _executor.ReorganizeIndexAsync(index, ActionTrigger.Automatic, false, ct);
            }
        }
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromMinutes(_monOptions.FragmentationIntervalMinutes), ct);
}
