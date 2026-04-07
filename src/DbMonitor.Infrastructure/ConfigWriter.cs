using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
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
}
