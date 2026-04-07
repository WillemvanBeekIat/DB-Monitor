using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int count = 50, CancellationToken ct = default);
}
