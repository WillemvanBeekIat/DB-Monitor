using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Infrastructure;

public class ConfigWriter : IConfigWriter
{
    private readonly string _appSettingsPath;
    private readonly ILogger<ConfigWriter> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ConfigWriter(string appSettingsPath, ILogger<ConfigWriter> logger)
    {
        _appSettingsPath = appSettingsPath;
        _logger = logger;
    }

    public async Task SetDryRunAsync(string section, bool value, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_appSettingsPath, ct);
            var node = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException("appsettings.json is not a JSON object");

            if (node[section] is JsonObject sectionObj)
                sectionObj["DryRun"] = value;
            else
            {
                node[section] = new JsonObject { ["DryRun"] = value };
            }

            var updated = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_appSettingsPath, updated, ct);
            _logger.LogInformation("Updated {Section}.DryRun = {Value} in appsettings.json", section, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update DryRun in appsettings.json");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddMonitoredIndexesAsync(IEnumerable<MonitoredIndexConfig> indexes, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_appSettingsPath, ct);
            var root = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException("appsettings.json is not a JSON object");

            const string section = "Fragmentation";
            if (root[section] is not JsonObject fragSection)
            {
                fragSection = new JsonObject();
                root[section] = fragSection;
            }

            if (fragSection["MonitoredIndexes"] is not JsonArray existing)
            {
                existing = new JsonArray();
                fragSection["MonitoredIndexes"] = existing;
            }

            // Collect keys already present to avoid duplicates
            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in existing)
            {
                if (item is JsonObject obj)
                {
                    var key = BuildKey(
                        obj["Database"]?.GetValue<string>() ?? string.Empty,
                        obj["Schema"]?.GetValue<string>() ?? string.Empty,
                        obj["Table"]?.GetValue<string>() ?? string.Empty,
                        obj["Index"]?.GetValue<string>() ?? string.Empty);
                    existingKeys.Add(key);
                }
            }

            int added = 0;
            foreach (var idx in indexes)
            {
                var key = BuildKey(idx.Database, idx.Schema, idx.Table, idx.Index);
                if (existingKeys.Contains(key)) continue;

                existing.Add(JsonSerializer.SerializeToNode(idx, new JsonSerializerOptions { WriteIndented = false }));
                existingKeys.Add(key);
                added++;
            }

            if (added > 0)
            {
                var updated = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_appSettingsPath, updated, ct);
                _logger.LogInformation("Added {Count} new monitored index(es) to appsettings.json", added);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add monitored indexes to appsettings.json");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string BuildKey(string db, string schema, string table, string index)
        => $"{db}.{schema}.{table}.{index}";

    public async Task PatchSectionAsync(string section, IReadOnlyDictionary<string, JsonNode?> fields, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_appSettingsPath, ct);
            var node = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException("appsettings.json is not a JSON object");

            if (node[section] is not JsonObject sectionObj)
            {
                sectionObj = new JsonObject();
                node[section] = sectionObj;
            }

            foreach (var (key, value) in fields)
                sectionObj[key] = value?.DeepClone();

            var updated = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_appSettingsPath, updated, ct);
            _logger.LogInformation("Patched section {Section} with {Count} field(s)", section, fields.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to patch section {Section}", section);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}

