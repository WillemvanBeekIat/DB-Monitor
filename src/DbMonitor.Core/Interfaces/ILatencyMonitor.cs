using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface ILatencyMonitor
{
    Task<LatencySnapshot> MeasureAsync(CancellationToken ct = default);
    IReadOnlyList<LatencySnapshot> GetHistory();
}
