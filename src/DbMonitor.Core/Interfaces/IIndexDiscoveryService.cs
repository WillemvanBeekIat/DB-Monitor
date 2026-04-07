using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IIndexDiscoveryService
{
    Task<IReadOnlyList<DiscoverableIndex>> DiscoverAsync(CancellationToken ct = default);
}
