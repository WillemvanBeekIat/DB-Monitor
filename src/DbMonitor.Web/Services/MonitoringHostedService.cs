using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Web.Services;

public abstract class MonitoringHostedService : BackgroundService
{
    protected readonly ILogger Logger;
    protected readonly string ServiceName;

    protected MonitoringHostedService(string serviceName, ILogger logger)
    {
        ServiceName = serviceName;
        Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("{Service} starting", ServiceName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{Service} iteration failed", ServiceName);
            }

            await DelayAsync(stoppingToken);
        }

        Logger.LogInformation("{Service} stopping", ServiceName);
    }

    protected abstract Task RunIterationAsync(CancellationToken ct);
    protected abstract Task DelayAsync(CancellationToken ct);
}
