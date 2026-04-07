using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface IHealthAggregator
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default);
    DashboardSummary? LastSummary { get; }
}
