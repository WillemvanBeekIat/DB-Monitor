using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
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

    [Fact]
    public async Task AddMonitoredIndexesAsync_AddsIndexToEmptyArray()
    {
        WriteConfig("""{"Fragmentation":{"MonitoredIndexes":[]}}""");

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.AddMonitoredIndexesAsync(new[]
        {
            new MonitoredIndexConfig
            {
                Database = "MyDb",
                Schema = "dbo",
                Table = "Orders",
                Index = "IX_Orders_Date",
                Enabled = true,
                FragmentationPercentThreshold = 30.0
            }
        });

        var json = File.ReadAllText(_configPath);
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("Fragmentation").GetProperty("MonitoredIndexes");

        Assert.Equal(1, arr.GetArrayLength());
        var first = arr[0];
        Assert.Equal("MyDb", first.GetProperty("Database").GetString());
        Assert.Equal("Orders", first.GetProperty("Table").GetString());
        Assert.Equal("IX_Orders_Date", first.GetProperty("Index").GetString());
    }

    [Fact]
    public async Task AddMonitoredIndexesAsync_CreatesSectionWhenMissing()
    {
        WriteConfig("{}");

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.AddMonitoredIndexesAsync(new[]
        {
            new MonitoredIndexConfig { Database = "Db1", Schema = "dbo", Table = "T1", Index = "IX_T1" }
        });

        var json = File.ReadAllText(_configPath);
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("Fragmentation").GetProperty("MonitoredIndexes");

        Assert.Equal(1, arr.GetArrayLength());
    }

    [Fact]
    public async Task AddMonitoredIndexesAsync_DoesNotAddDuplicates()
    {
        WriteConfig("""
        {
          "Fragmentation": {
            "MonitoredIndexes": [
              {"Database":"Db1","Schema":"dbo","Table":"T1","Index":"IX_T1","Enabled":true,"FragmentationPercentThreshold":30,"MinimumPageCount":1000,"MinimumUsageScore":0,"AllowReorganize":true,"UsageWeights":{"SeekWeight":1,"ScanWeight":0.5,"LookupWeight":0.8,"UpdateWeight":-0.2}}
            ]
          }
        }
        """);

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.AddMonitoredIndexesAsync(new[]
        {
            new MonitoredIndexConfig { Database = "Db1", Schema = "dbo", Table = "T1", Index = "IX_T1" }
        });

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        var arr = doc.RootElement.GetProperty("Fragmentation").GetProperty("MonitoredIndexes");

        Assert.Equal(1, arr.GetArrayLength());
    }

    [Fact]
    public async Task AddMonitoredIndexesAsync_AddsMultipleIndexes()
    {
        WriteConfig("""{"Fragmentation":{"MonitoredIndexes":[]}}""");

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.AddMonitoredIndexesAsync(new[]
        {
            new MonitoredIndexConfig { Database = "Db1", Schema = "dbo", Table = "T1", Index = "IX_A" },
            new MonitoredIndexConfig { Database = "Db1", Schema = "dbo", Table = "T1", Index = "IX_B" }
        });

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        var arr = doc.RootElement.GetProperty("Fragmentation").GetProperty("MonitoredIndexes");

        Assert.Equal(2, arr.GetArrayLength());
    }

    [Fact]
    public async Task AddMonitoredIndexesAsync_PreservesExistingSettings()
    {
        WriteConfig("""
        {
          "Fragmentation": {
            "Enabled": true,
            "DryRun": false,
            "MonitoredIndexes": []
          },
          "SqlServer": { "ConnectionString": "Server=test" }
        }
        """);

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.AddMonitoredIndexesAsync(new[]
        {
            new MonitoredIndexConfig { Database = "Db1", Schema = "dbo", Table = "T1", Index = "IX_T1" }
        });

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        var frag = doc.RootElement.GetProperty("Fragmentation");

        Assert.True(frag.GetProperty("Enabled").GetBoolean());
        Assert.False(frag.GetProperty("DryRun").GetBoolean());
        Assert.Equal("Server=test", doc.RootElement.GetProperty("SqlServer").GetProperty("ConnectionString").GetString());
        Assert.Equal(1, frag.GetProperty("MonitoredIndexes").GetArrayLength());
    }

    [Fact]
    public async Task PatchSectionAsync_UpdatesExistingFields()
    {
        WriteConfig("""
        {
          "SqlServer": {
            "ConnectionString": "Server=old",
            "ConnectionTimeoutSeconds": 10,
            "CommandTimeoutSeconds": 30
          }
        }
        """);

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.PatchSectionAsync("SqlServer", new Dictionary<string, System.Text.Json.Nodes.JsonNode?>
        {
            ["ConnectionTimeoutSeconds"] = System.Text.Json.Nodes.JsonValue.Create(20),
            ["CommandTimeoutSeconds"] = System.Text.Json.Nodes.JsonValue.Create(60),
        });

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        var sql = doc.RootElement.GetProperty("SqlServer");
        Assert.Equal("Server=old", sql.GetProperty("ConnectionString").GetString());
        Assert.Equal(20, sql.GetProperty("ConnectionTimeoutSeconds").GetInt32());
        Assert.Equal(60, sql.GetProperty("CommandTimeoutSeconds").GetInt32());
    }

    [Fact]
    public async Task PatchSectionAsync_CreatesSection_WhenMissing()
    {
        WriteConfig("{}");

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.PatchSectionAsync("Latency", new Dictionary<string, System.Text.Json.Nodes.JsonNode?>
        {
            ["WarningMs"] = System.Text.Json.Nodes.JsonValue.Create(300.0),
            ["CriticalMs"] = System.Text.Json.Nodes.JsonValue.Create(1500.0),
        });

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        var lat = doc.RootElement.GetProperty("Latency");
        Assert.Equal(300.0, lat.GetProperty("WarningMs").GetDouble());
        Assert.Equal(1500.0, lat.GetProperty("CriticalMs").GetDouble());
    }

    [Fact]
    public async Task PatchSectionAsync_PreservesOtherSections()
    {
        WriteConfig("""
        {
          "Monitoring": { "InstanceProbeIntervalSeconds": 30, "LatencyIntervalSeconds": 30 },
          "SqlServer": { "ConnectionString": "Server=test" }
        }
        """);

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.PatchSectionAsync("Monitoring", new Dictionary<string, System.Text.Json.Nodes.JsonNode?>
        {
            ["InstanceProbeIntervalSeconds"] = System.Text.Json.Nodes.JsonValue.Create(60),
        });

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        Assert.Equal(60, doc.RootElement.GetProperty("Monitoring").GetProperty("InstanceProbeIntervalSeconds").GetInt32());
        Assert.Equal(30, doc.RootElement.GetProperty("Monitoring").GetProperty("LatencyIntervalSeconds").GetInt32());
        Assert.Equal("Server=test", doc.RootElement.GetProperty("SqlServer").GetProperty("ConnectionString").GetString());
    }

    [Fact]
    public async Task PatchSectionAsync_CanWriteStringArrayField()
    {
        WriteConfig("""{"SqlServer":{"ExcludedDatabases":["tempdb"]}}""");

        var writer = new ConfigWriter(_configPath, NullLogger<ConfigWriter>.Instance);
        await writer.PatchSectionAsync("SqlServer", new Dictionary<string, System.Text.Json.Nodes.JsonNode?>
        {
            ["ExcludedDatabases"] = System.Text.Json.JsonSerializer.SerializeToNode(new[] { "tempdb", "model" }),
        });

        var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
        var arr = doc.RootElement.GetProperty("SqlServer").GetProperty("ExcludedDatabases");
        Assert.Equal(2, arr.GetArrayLength());
        Assert.Equal("tempdb", arr[0].GetString());
        Assert.Equal("model", arr[1].GetString());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* ignore */ }
    }
}
