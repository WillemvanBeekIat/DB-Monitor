using System.Collections.Generic;

namespace DbMonitor.Core.Configuration;

public class FragmentationOptions
{
    public const string SectionName = "Fragmentation";
    public bool Enabled { get; set; } = true;
    public bool DryRun { get; set; } = true;
    public int MaxConcurrentActions { get; set; } = 1;
    public int CooldownMinutes { get; set; } = 60;
    public List<MonitoredIndexConfig> MonitoredIndexes { get; set; } = new();
}

public class MonitoredIndexConfig
{
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string Table { get; set; } = string.Empty;
    public string Index { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public double FragmentationPercentThreshold { get; set; } = 30.0;
    public int MinimumPageCount { get; set; } = 1000;
    public double MinimumUsageScore { get; set; } = 0.0;
    public bool AllowReorganize { get; set; } = true;
    public IndexUsageWeights UsageWeights { get; set; } = new();
}

public class IndexUsageWeights
{
    public double SeekWeight { get; set; } = 1.0;
    public double ScanWeight { get; set; } = 0.5;
    public double LookupWeight { get; set; } = 0.8;
    public double UpdateWeight { get; set; } = -0.2;
}
