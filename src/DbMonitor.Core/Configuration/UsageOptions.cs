namespace DbMonitor.Core.Configuration;

public class UsageOptions
{
    public const string SectionName = "Usage";
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 30;
    public string SnapshotStoragePath { get; set; } = "data/usage";
}
