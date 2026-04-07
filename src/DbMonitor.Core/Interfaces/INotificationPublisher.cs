using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(string title, string message, HealthStatus severity, CancellationToken ct = default);
}
