namespace DbMonitor.Core.Configuration;

public class UiOptions
{
    public const string SectionName = "Ui";
    public string DefaultTheme { get; set; } = "Dark";
    public bool AllowThemeToggle { get; set; } = true;
    public int AutoRefreshDefaultSeconds { get; set; } = 300;
    public bool MobileFirst { get; set; } = true;
}
