using System.Collections.Generic;

namespace DbMonitor.Core.Configuration;

public class ErrorLogOptions
{
    public const string SectionName = "ErrorLog";
    public bool Enabled { get; set; } = true;
    public int SeverityThreshold { get; set; } = 17;
    public List<string> Keywords { get; set; } = new() { "error", "corrupt", "deadlock", "I/O", "failed", "severe" };
    public string BookmarkStoragePath { get; set; } = "data/bookmarks";
}
