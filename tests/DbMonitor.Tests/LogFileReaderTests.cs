using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DbMonitor.Core.Configuration;
using DbMonitor.Infrastructure.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DbMonitor.Tests;

public class LogFileReaderTests
{
    private static JsonLogFileReader Create() =>
        new(Options.Create(new LoggingOptions
        {
            LogDirectory = "logs",
            CurrentLogFileNamePattern = "dbmonitor-{Date}.jsonl"
        }));

    [Fact]
    public async Task ReadEventsAsync_ParsesValidJsonl()
    {
        var reader = Create();
        var lines = """
        {"@t":"2024-01-15T10:00:00+00:00","@mt":"Test message","@l":"Information","EventType":"HealthCheck"}
        {"@t":"2024-01-15T10:01:00+00:00","@mt":"Warning occurred","@l":"Warning","EventType":"Latency"}
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(lines));
        var events = await reader.ReadEventsAsync(stream);

        Assert.Equal(2, events.Count);
        Assert.Equal("Information", events[0].Level);
        Assert.Equal("HealthCheck", events[0].EventType);
        Assert.Equal("Warning", events[1].Level);
    }

    [Fact]
    public async Task ReadEventsAsync_SkipsMalformedLines()
    {
        var reader = Create();
        var lines = """
        {"@t":"2024-01-15T10:00:00+00:00","@mt":"Good line","@l":"Information"}
        THIS IS NOT JSON
        {"@t":"2024-01-15T10:02:00+00:00","@mt":"Another good line","@l":"Warning"}
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(lines));
        var events = await reader.ReadEventsAsync(stream);

        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ReadEventsAsync_EmptyStream_ReturnsEmpty()
    {
        var reader = Create();
        using var stream = new MemoryStream();
        var events = await reader.ReadEventsAsync(stream);
        Assert.Empty(events);
    }

    [Fact]
    public void GetCurrentLogFilePath_ContainsTodaysDate()
    {
        var reader = Create();
        var path = reader.GetCurrentLogFilePath();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        Assert.Contains(today, path);
        Assert.Contains("dbmonitor-", path);
    }
}
