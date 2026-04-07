using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DbMonitor.Infrastructure.Logging;

public class StructuredLogWriter
{
    private readonly LoggingOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    public StructuredLogWriter(IOptions<LoggingOptions> options)
    {
        _options = options.Value;
        Directory.CreateDirectory(_options.LogDirectory);
    }

    public async Task WriteEventAsync(string eventType, string message, object? properties = null, CancellationToken ct = default)
    {
        var entry = new
        {
            @t = DateTimeOffset.UtcNow,
            @mt = message,
            @m = message,
            @l = "Information",
            EventType = eventType,
            Properties = properties
        };

        var path = GetCurrentLogPath();
        var line = JsonSerializer.Serialize(entry, _jsonOpts);

        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetCurrentLogPath()
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var fileName = _options.CurrentLogFileNamePattern.Replace("{Date}", date);
        return Path.Combine(_options.LogDirectory, fileName);
    }
}
