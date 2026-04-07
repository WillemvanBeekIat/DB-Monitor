using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IStateStore
{
    Task SaveInstanceHealthAsync(InstanceHealth health, CancellationToken ct = default);
    Task<InstanceHealth?> LoadInstanceHealthAsync(CancellationToken ct = default);
    Task SaveCooldownAsync(string key, System.DateTimeOffset until, CancellationToken ct = default);
    Task<System.DateTimeOffset?> LoadCooldownAsync(string key, CancellationToken ct = default);
    Task SaveErrorBookmarkAsync(string bookmark, CancellationToken ct = default);
    Task<string?> LoadErrorBookmarkAsync(CancellationToken ct = default);
}
