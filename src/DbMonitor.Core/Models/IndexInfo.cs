using System;

namespace DbMonitor.Core.Models;

public class IndexInfo
{
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public double FragmentationPercent { get; set; }
    public long PageCount { get; set; }
    public double UsageScore { get; set; }
    public bool IsEligible { get; set; }
    public string? IneligibilityReason { get; set; }
    public DateTimeOffset LastChecked { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReorganized { get; set; }
}
