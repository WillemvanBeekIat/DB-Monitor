using System;

namespace DbMonitor.Core.Models;

public enum SqlErrorCategory { Severe, IO, Corruption, Deadlock, Resource, Other }

public class SqlErrorEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public int? ErrorNumber { get; set; }
    public int? Severity { get; set; }
    public string? Message { get; set; }
    public SqlErrorCategory Category { get; set; }
    public string? Source { get; set; }
}
