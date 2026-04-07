using System;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbMonitor.Web.Services;

public class ErrorLogHostedService : MonitoringHostedService
{
    private readonly IErrorLogMonitor _monitor;
    private readonly MonitoringOptions _options;

    public ErrorLogHostedService(
        IErrorLogMonitor monitor,
        IOptions<MonitoringOptions> options,
        ILogger<ErrorLogHostedService> logger)
        : base("ErrorLogMonitor", logger)
    {
        _monitor = monitor;
        _options = options.Value;
    }

    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        var errors = await _monitor.ReadNewErrorsAsync(ct);
        if (errors.Count > 0)
            Logger.LogWarning("SQL Server error log: {Count} new entries detected", errors.Count);
    }

    protected override Task DelayAsync(CancellationToken ct) =>
        Task.Delay(TimeSpan.FromSeconds(_options.ErrorLogIntervalSeconds), ct);
}
