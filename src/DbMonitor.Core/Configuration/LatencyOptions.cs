namespace DbMonitor.Core.Configuration;

public class LatencyOptions
{
    public const string SectionName = "Latency";
    public double WarningMs { get; set; } = 500;
    public double CriticalMs { get; set; } = 2000;
    public int MovingAverageWindow { get; set; } = 10;
}
