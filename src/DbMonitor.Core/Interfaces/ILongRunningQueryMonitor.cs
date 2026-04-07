using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface ILongRunningQueryMonitor
{
    Task<IReadOnlyList<LongRunningQuery>> GetLongRunningQueriesAsync(CancellationToken ct = default);
}
