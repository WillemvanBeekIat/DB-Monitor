using System;

namespace DbMonitor.Core.Models;

public class LatencySnapshot
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public double ConnectionOpenMs { get; set; }
    public double ProbeQueryMs { get; set; }
    public double? MetadataQueryMs { get; set; }
    public HealthStatus Status { get; set; }
}
