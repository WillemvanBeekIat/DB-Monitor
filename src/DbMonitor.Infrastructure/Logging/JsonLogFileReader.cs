using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using Microsoft.Extensions.Options;

namespace DbMonitor.Infrastructure.Logging;

public class JsonLogFileReader : ILogFileReader
{
    private readonly LoggingOptions _options;

    public JsonLogFileReader(IOptions<LoggingOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IReadOnlyList<LogEvent>> ReadEventsAsync(Stream stream, CancellationToken ct = default)
    {
        var result = new List<LogEvent>();

        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var ev = JsonSerializer.Deserialize<LogEvent>(line);
                if (ev != null) result.Add(ev);
            }
            catch { /* skip malformed lines */ }
        }

        return result;
    }

    public string GetCurrentLogFilePath()
    {
        var dir = _options.LogDirectory;
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var fileName = _options.CurrentLogFileNamePattern.Replace("{Date}", date);
        return Path.Combine(dir, fileName);
    }

    public IReadOnlyList<string> GetAvailableLogFiles()
    {
        var dir = _options.LogDirectory;
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        var files = Directory.GetFiles(dir, "*.jsonl");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        return files;
    }
}
