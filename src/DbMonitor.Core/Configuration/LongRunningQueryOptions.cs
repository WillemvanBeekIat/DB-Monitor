using System.Collections.Generic;

namespace DbMonitor.Core.Configuration;

public class LongRunningQueryOptions
{
    public const string SectionName = "LongRunningQueries";
    public bool Enabled { get; set; } = true;
    public bool DryRun { get; set; } = true;
    public bool KillEnabled { get; set; } = false;
    public int ThresholdSeconds { get; set; } = 300;
    public int CooldownMinutes { get; set; } = 5;
    public List<string> AllowedLogins { get; set; } = new();
    public List<string> AllowedHosts { get; set; } = new();
    public List<string> AllowedPrograms { get; set; } = new();
    public List<string> IgnoredCommands { get; set; } = new();
    public List<string> IncludedDatabases { get; set; } = new();
    public List<string> ExcludedDatabases { get; set; } = new();
    public bool IgnoreSqlAgentJobs { get; set; } = true;
    public bool IgnoreBackups { get; set; } = true;
    public bool IgnoreRestores { get; set; } = true;
    public bool IgnoreCheckDb { get; set; } = true;
    public bool IgnoreIndexMaintenance { get; set; } = true;
}
