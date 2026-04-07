using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IFragmentationMonitor
{
    Task<IReadOnlyList<IndexInfo>> CheckAsync(CancellationToken ct = default);
}
