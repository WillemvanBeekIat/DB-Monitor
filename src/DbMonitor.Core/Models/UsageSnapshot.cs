using System;
using System.Collections.Generic;

namespace DbMonitor.Core.Models;

public class UsageSnapshot
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public List<ObjectUsage> Objects { get; set; } = new();
}

public class ObjectUsage
{
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string? IndexName { get; set; }
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public long UserLookups { get; set; }
    public long UserUpdates { get; set; }
    public double CompositeScore { get; set; }
    public DateTimeOffset LastAccess { get; set; }
}
