namespace DbMonitor.Core.Configuration;

public class LoggingOptions
{
    public const string SectionName = "AppLogging";
    public string MinimumLevel { get; set; } = "Information";
    public string LogDirectory { get; set; } = "logs";
    public string CurrentLogFileNamePattern { get; set; } = "dbmonitor-{Date}.jsonl";
    public bool JsonStructuredLogging { get; set; } = true;
    public string RollingStrategy { get; set; } = "Day";
    public int RetentionDays { get; set; } = 30;
}
