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
    private readonly IOptionsMonitor<FragmentationOptions> _options;
    private readonly IOptionsMonitor<MonitoringOptions> _monOptions;

    public FragmentationHostedService(
        IFragmentationMonitor monitor,
        IActionExecutor executor,
        IOptionsMonitor<FragmentationOptions> options,
        IOptionsMonitor<MonitoringOptions> monOptions,
        ILogger<FragmentationHostedService> logger)
        : base("FragmentationMonitor", logger)
    {
        _monitor = monitor;
        _executor = executor;
        _options = options;
        _monOptions = monOptions;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var indexes = await _monitor.CheckAsync(ct);
        var eligible = indexes.Where(i => i.IsEligible).ToList();

        var opts = _options.CurrentValue;
        Logger.LogInformation("Fragmentation check: {Total} monitored, {Eligible} eligible",
            indexes.Count, eligible.Count);

        if (opts.Enabled && eligible.Count > 0)
        {
            var toProcess = eligible.Take(opts.MaxConcurrentActions).ToList();
            foreach (var index in toProcess)
            {
                await _executor.ReorganizeIndexAsync(index, ActionTrigger.Automatic, false, ct);
            }
        }
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromMinutes(_monOptions.CurrentValue.FragmentationIntervalMinutes), ct);
}
