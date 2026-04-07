namespace DbMonitor.Core.Configuration;

public class NotificationOptions
{
    public const string SectionName = "Notifications";
    public bool Enabled { get; set; } = false;
    public bool UseWebhook { get; set; } = false;
    public string WebhookUrl { get; set; } = string.Empty;
    public string MinimumLevel { get; set; } = "Warning";
}
