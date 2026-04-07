using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Infrastructure.Persistence;

public class JsonStateStore : IStateStore
{
    private readonly string _stateDir;
    private readonly ILogger<JsonStateStore> _logger;
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    public JsonStateStore(string stateDir, ILogger<JsonStateStore> logger)
    {
        _stateDir = stateDir;
        Directory.CreateDirectory(stateDir);
        _logger = logger;
    }

    public async Task SaveInstanceHealthAsync(InstanceHealth health, CancellationToken ct = default)
    {
        var path = Path.Combine(_stateDir, "instance-health.json");
        await WriteJsonAsync(path, health, ct);
    }

    public async Task<InstanceHealth?> LoadInstanceHealthAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(_stateDir, "instance-health.json");
        return await ReadJsonAsync<InstanceHealth>(path, ct);
    }

    public async Task SaveCooldownAsync(string key, DateTimeOffset until, CancellationToken ct = default)
    {
        var safeKey = MakeSafeFileName(key);
        var path = Path.Combine(_stateDir, $"cooldown-{safeKey}.json");
        await WriteJsonAsync(path, until, ct);
    }

    public async Task<DateTimeOffset?> LoadCooldownAsync(string key, CancellationToken ct = default)
    {
        var safeKey = MakeSafeFileName(key);
        var path = Path.Combine(_stateDir, $"cooldown-{safeKey}.json");
        if (!File.Exists(path)) return null;
        return await ReadJsonAsync<DateTimeOffset>(path, ct);
    }

    public async Task SaveErrorBookmarkAsync(string bookmark, CancellationToken ct = default)
    {
        var path = Path.Combine(_stateDir, "error-log-bookmark.txt");
        await File.WriteAllTextAsync(path, bookmark, ct);
    }

    public async Task<string?> LoadErrorBookmarkAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(_stateDir, "error-log-bookmark.txt");
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    private async Task WriteJsonAsync<T>(string path, T value, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOpts);
            await File.WriteAllTextAsync(path, json, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write state to {Path}", path);
        }
    }

    private async Task<T?> ReadJsonAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return default;
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read state from {Path}", path);
            return default;
        }
    }

    private static string MakeSafeFileName(string key)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            key = key.Replace(c, '_');
        return key.Replace(':', '_').Replace('.', '_');
    }
}
