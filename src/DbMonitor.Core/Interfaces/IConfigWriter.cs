using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;

namespace DbMonitor.Core.Interfaces;

public interface IConfigWriter
{
    Task SetDryRunAsync(string section, bool value, CancellationToken ct = default);
    Task AddMonitoredIndexesAsync(IEnumerable<MonitoredIndexConfig> indexes, CancellationToken ct = default);
    Task PatchSectionAsync(string section, IReadOnlyDictionary<string, JsonNode?> fields, CancellationToken ct = default);
}
