using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DbMonitor.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DbMonitor.Tests;

public class ConfigWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dbmonitor-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "appsettings.json");
    }

    private void WriteConfig(string json) => File.WriteAllText(_configPath, json);

    [Fact]
    public async Task SetDryRunAsync_UpdatesExistingSection()
    {
        WriteConfig("""
        {
          "Fragmentation": {
            "DryRun": true,
            "Enabled": true
          }
        }
        """);

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.SetDryRunAsync("Fragmentation", false);

        var json = File.ReadAllText(_configPath);
        var doc = JsonDocument.Parse(json);
        var value = doc.RootElement
            .GetProperty("Fragmentation")
            .GetProperty("DryRun")
            .GetBoolean();

        Assert.False(value);
    }

    [Fact]
    public async Task SetDryRunAsync_CreatesSection_WhenMissing()
    {
        WriteConfig("{}");

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.SetDryRunAsync("Fragmentation", true);

        var json = File.ReadAllText(_configPath);
        var doc = JsonDocument.Parse(json);
        var value = doc.RootElement
            .GetProperty("Fragmentation")
            .GetProperty("DryRun")
            .GetBoolean();

        Assert.True(value);
    }

    [Fact]
    public async Task SetDryRunAsync_PreservesOtherSettings()
    {
        WriteConfig("""
        {
          "Fragmentation": {
            "DryRun": true,
            "Enabled": true,
            "CooldownMinutes": 60
          },
          "SqlServer": {
            "ConnectionString": "Server=test"
          }
        }
        """);

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.SetDryRunAsync("Fragmentation", false);

        var json = File.ReadAllText(_configPath);
        var doc = JsonDocument.Parse(json);
        var frag = doc.RootElement.GetProperty("Fragmentation");

        Assert.False(frag.GetProperty("DryRun").GetBoolean());
        Assert.True(frag.GetProperty("Enabled").GetBoolean());
        Assert.Equal(60, frag.GetProperty("CooldownMinutes").GetInt32());
        Assert.Equal("Server=test", doc.RootElement.GetProperty("SqlServer").GetProperty("ConnectionString").GetString());
    }

    [Fact]
    public async Task SetDryRunAsync_ToggleTrueToFalseToTrue_WorksCorrectly()
    {
        WriteConfig("""{"Fragmentation":{"DryRun":false}}""");

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.SetDryRunAsync("Fragmentation", true);

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        Assert.True(doc.RootElement.GetProperty("Fragmentation").GetProperty("DryRun").GetBoolean());

        await writer.SetDryRunAsync("Fragmentation", false);

        doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        Assert.False(doc.RootElement.GetProperty("Fragmentation").GetProperty("DryRun").GetBoolean());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* ignore */ }
    }
}
