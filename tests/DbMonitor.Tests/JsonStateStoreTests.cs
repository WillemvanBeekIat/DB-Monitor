using System;
using System.IO;
using System.Threading.Tasks;
using DbMonitor.Core.Models;
using DbMonitor.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DbMonitor.Tests;

public class JsonStateStoreTests : IDisposable
{
    private readonly string _dir;

    public JsonStateStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dbmonitor-state-test-{Guid.NewGuid()}");
    }

    private JsonStateStore Create() => new(_dir, NullLogger<JsonStateStore>.Instance);

    [Fact]
    public async Task SaveAndLoad_InstanceHealth_RoundTrips()
    {
        var store = Create();
        var health = new InstanceHealth
        {
            IsReachable = true,
            ConsecutiveFailures = 0,
            ConnectionOpenMs = 12.5,
            ProbeQueryMs = 8.3,
            Status = HealthStatus.Healthy
        };

        await store.SaveInstanceHealthAsync(health);
        var loaded = await store.LoadInstanceHealthAsync();

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsReachable);
        Assert.Equal(12.5, loaded.ConnectionOpenMs);
    }

    [Fact]
    public async Task LoadInstanceHealth_WhenFileNotExists_ReturnsNull()
    {
        var store = Create();
        var result = await store.LoadInstanceHealthAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoad_Cooldown_RoundTrips()
    {
        var store = Create();
        var until = DateTimeOffset.UtcNow.AddMinutes(30);

        await store.SaveCooldownAsync("test-key", until);
        var loaded = await store.LoadCooldownAsync("test-key");

        Assert.NotNull(loaded);
        Assert.Equal(until.ToUnixTimeSeconds(), loaded!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task LoadCooldown_WhenFileNotExists_ReturnsNull()
    {
        var store = Create();
        var result = await store.LoadCooldownAsync("nonexistent-key");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoad_ErrorBookmark_RoundTrips()
    {
        var store = Create();
        var bookmark = "2024-01-15T10:30:00+00:00";

        await store.SaveErrorBookmarkAsync(bookmark);
        var loaded = await store.LoadErrorBookmarkAsync();

        Assert.Equal(bookmark, loaded);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* ignore */ }
    }
}
