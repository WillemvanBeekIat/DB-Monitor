using System;
using System.Collections.Generic;

namespace DbMonitor.Core.Models;

public class DashboardSummary
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public HealthStatus OverallStatus { get; set; }
    public InstanceHealth? InstanceHealth { get; set; }
    public int LongRunningQueryCount { get; set; }
    public int ActionableFragmentedIndexCount { get; set; }
    public int InternalErrorCount { get; set; }
    public List<AuditEntry> RecentActions { get; set; } = new();
    public bool IsDryRunEnabled { get; set; }
    public List<LatencySnapshot> RecentLatency { get; set; } = new();
}
