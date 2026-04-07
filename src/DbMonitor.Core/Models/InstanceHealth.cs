using System;
using System.Collections.Generic;

namespace DbMonitor.Core.Models;

public class InstanceHealth
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool IsReachable { get; set; }
    public HealthStatus Status { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double? ConnectionOpenMs { get; set; }
    public double? ProbeQueryMs { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DatabaseHealth> Databases { get; set; } = new();
}

public class DatabaseHealth
{
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsReachable { get; set; }
    public string? State { get; set; }
    public string? ErrorMessage { get; set; }
}
