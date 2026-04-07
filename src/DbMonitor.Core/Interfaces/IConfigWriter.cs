using System.Threading;
using System.Threading.Tasks;

namespace DbMonitor.Core.Interfaces;

public interface IConfigWriter
{
    Task SetDryRunAsync(string section, bool value, CancellationToken ct = default);
}
