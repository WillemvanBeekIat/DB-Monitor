using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;

namespace DbMonitor.Infrastructure.Notifications;

public class NullNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync(string title, string message, HealthStatus severity, CancellationToken ct = default)
        => Task.CompletedTask;
}
