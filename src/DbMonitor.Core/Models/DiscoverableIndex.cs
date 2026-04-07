namespace DbMonitor.Core.Models;

public class DiscoverableIndex
{
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public long PageCount { get; set; }
    public double FragmentationPercent { get; set; }
    public bool AlreadyMonitored { get; set; }
}
