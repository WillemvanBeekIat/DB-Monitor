using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Interfaces;

public interface ILogFileReader
{
    Task<IReadOnlyList<LogEvent>> ReadEventsAsync(Stream stream, CancellationToken ct = default);
    string GetCurrentLogFilePath();
    IReadOnlyList<string> GetAvailableLogFiles();
}
