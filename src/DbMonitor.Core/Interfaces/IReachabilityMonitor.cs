using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IReachabilityMonitor
{
    Task<InstanceHealth> CheckAsync(CancellationToken ct = default);
}
