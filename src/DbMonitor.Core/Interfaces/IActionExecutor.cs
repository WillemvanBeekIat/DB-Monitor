using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IActionExecutor
{
    Task<AuditEntry> ReorganizeIndexAsync(IndexInfo index, ActionTrigger trigger, bool manualOverride = false, CancellationToken ct = default);
    Task<AuditEntry> KillQueryAsync(LongRunningQuery query, ActionTrigger trigger, bool manualOverride = false, CancellationToken ct = default);
}
