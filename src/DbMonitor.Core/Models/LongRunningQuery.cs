using System;

namespace DbMonitor.Core.Models;

public class LongRunningQuery
{
    public int SessionId { get; set; }
    public string? LoginName { get; set; }
    public string? HostName { get; set; }
    public string? ProgramName { get; set; }
    public string? DatabaseName { get; set; }
    public string? Command { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public TimeSpan ElapsedDuration { get; set; }
    public string? WaitType { get; set; }
    public int? BlockingSessionId { get; set; }
    public string? SqlText { get; set; }
    public bool WouldBeKilled { get; set; }
    public string? SkipReason { get; set; }
}
