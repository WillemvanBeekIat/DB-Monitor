namespace DbMonitor.Core.Configuration;

public class ConfigMutationOptions
{
    public const string SectionName = "ConfigMutation";
    public bool AllowDryRunToggleWriteback { get; set; } = true;
    public bool AllowIndexImport { get; set; } = true;
    public bool AllowSettingsWriteback { get; set; } = true;
}
