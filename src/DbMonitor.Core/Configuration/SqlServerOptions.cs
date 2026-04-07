using System.Collections.Generic;

namespace DbMonitor.Core.Configuration;

public class SqlServerOptions
{
    public const string SectionName = "SqlServer";
    public string ConnectionString { get; set; } = string.Empty;
    public List<string> IncludedDatabases { get; set; } = new();
    public List<string> ExcludedDatabases { get; set; } = new();
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    public int CommandTimeoutSeconds { get; set; } = 30;
}
