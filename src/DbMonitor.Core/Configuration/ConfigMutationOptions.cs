namespace DbMonitor.Core.Configuration;

public class ConfigMutationOptions
{
    public const string SectionName = "ConfigMutation";
    public bool AllowDryRunToggleWriteback { get; set; } = true;
}
