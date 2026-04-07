using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IUsageMonitor
{
    Task<UsageSnapshot> CaptureAsync(CancellationToken ct = default);
}
