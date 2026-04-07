using System;

namespace DbMonitor.Infrastructure.Data;

public class CooldownEntity
{
    public string Key { get; set; } = string.Empty;
    public DateTimeOffset Until { get; set; }
}

public class BookmarkEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
