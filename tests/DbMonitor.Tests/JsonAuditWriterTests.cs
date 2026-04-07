using System;
using System.IO;
using System.Threading.Tasks;
using DbMonitor.Core.Models;
using DbMonitor.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DbMonitor.Tests;

public class JsonAuditWriterTests : IDisposable
{
    private readonly string _dir;

    public JsonAuditWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dbmonitor-audit-test-{Guid.NewGuid()}");
    }

    private JsonAuditWriter Create() => new(_dir, NullLogger<JsonAuditWriter>.Instance);

    [Fact]
    public async Task WriteAsync_CreatesFile()
    {
        var writer = Create();
        await writer.WriteAsync(new AuditEntry
        {
            ActionType = ActionType.ReorganizeIndex,
            Target = "MyDb.dbo.Orders.IX_Test",
            Reason = "High fragmentation",
            Outcome = ActionOutcome.DryRun
        });

        var files = Directory.GetFiles(_dir, "*.jsonl");
        Assert.Single(files);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsWrittenEntries()
    {
        var writer = Create();

        for (int i = 0; i < 5; i++)
        {
            await writer.WriteAsync(new AuditEntry
            {
                ActionType = ActionType.KillQuery,
                Target = $"Session {i}",
                Reason = $"Reason {i}",
                Outcome = ActionOutcome.Executed
            });
        }

        var entries = await writer.GetRecentAsync(10);

        Assert.Equal(5, entries.Count);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsCount()
    {
        var writer = Create();

        for (int i = 0; i < 20; i++)
        {
            await writer.WriteAsync(new AuditEntry
            {
                Target = $"Target{i}",
                Reason = "test",
                Outcome = ActionOutcome.DryRun
            });
        }

        var entries = await writer.GetRecentAsync(5);
        Assert.True(entries.Count <= 5);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* ignore */ }
    }
}
