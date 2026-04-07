using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Infrastructure.Persistence;

public class JsonAuditWriter : IAuditWriter
{
    private readonly string _auditDir;
    private readonly ILogger<JsonAuditWriter> _logger;
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = false };
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonAuditWriter(string auditDir, ILogger<JsonAuditWriter> logger)
    {
        _auditDir = auditDir;
        Directory.CreateDirectory(auditDir);
        _logger = logger;
    }

    public async Task WriteAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var path = GetTodayPath();
        var line = JsonSerializer.Serialize(entry, _opts);

        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit entry");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int count = 50, CancellationToken ct = default)
    {
        var result = new List<AuditEntry>();
        var path = GetTodayPath();

        if (!File.Exists(path)) return result;

        try
        {
            var lines = await File.ReadAllLinesAsync(path, ct);
            foreach (var line in lines.Reverse().Take(count * 2))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<AuditEntry>(line);
                    if (entry != null) result.Add(entry);
                    if (result.Count >= count) break;
                }
                catch { /* skip bad lines */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read audit log");
        }

        result.Reverse();
        return result;
    }

    private string GetTodayPath() =>
        Path.Combine(_auditDir, $"audit-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
}
