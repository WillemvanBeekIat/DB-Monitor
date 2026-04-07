namespace DbMonitor.Core.Configuration;

public class MonitoringOptions
{
    public const string SectionName = "Monitoring";
    public int InstanceProbeIntervalSeconds { get; set; } = 30;
    public int DatabaseProbeIntervalSeconds { get; set; } = 60;
    public int LatencyIntervalSeconds { get; set; } = 30;
    public int FragmentationIntervalMinutes { get; set; } = 60;
    public int LongRunningQueryIntervalSeconds { get; set; } = 15;
    public int UsageIntervalMinutes { get; set; } = 30;
    public int ErrorLogIntervalSeconds { get; set; } = 60;
    public bool AutoRefreshEnabled { get; set; } = true;
    public int AutoRefreshSeconds { get; set; } = 300;
}
