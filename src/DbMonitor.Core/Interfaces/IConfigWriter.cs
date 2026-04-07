using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;

namespace DbMonitor.Core.Interfaces;

public interface IConfigWriter
{
    Task SetDryRunAsync(string section, bool value, CancellationToken ct = default);
    Task AddMonitoredIndexesAsync(IEnumerable<MonitoredIndexConfig> indexes, CancellationToken ct = default);
}
