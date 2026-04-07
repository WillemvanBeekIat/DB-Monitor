using System;
using System.Collections.Generic;
using System.Linq;
using DbMonitor.Core.Models;

namespace DbMonitor.Infrastructure.Data;

public class InstanceHealthEntity
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool IsReachable { get; set; }
    public HealthStatus Status { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double? ConnectionOpenMs { get; set; }
    public double? ProbeQueryMs { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCurrent { get; set; }
    public List<DatabaseHealthEntity> Databases { get; set; } = new();

    public static InstanceHealthEntity FromModel(InstanceHealth m) => new()
    {
        Timestamp = m.Timestamp,
        IsReachable = m.IsReachable,
        Status = m.Status,
        ConsecutiveFailures = m.ConsecutiveFailures,
        ConnectionOpenMs = m.ConnectionOpenMs,
        ProbeQueryMs = m.ProbeQueryMs,
        ErrorMessage = m.ErrorMessage,
        Databases = m.Databases.Select(d => DatabaseHealthEntity.FromModel(d)).ToList(),
    };

    public InstanceHealth ToModel() => new()
    {
        Timestamp = Timestamp,
        IsReachable = IsReachable,
        Status = Status,
        ConsecutiveFailures = ConsecutiveFailures,
        ConnectionOpenMs = ConnectionOpenMs,
        ProbeQueryMs = ProbeQueryMs,
        ErrorMessage = ErrorMessage,
        Databases = Databases.Select(d => d.ToModel()).ToList(),
    };
}

public class DatabaseHealthEntity
{
    public long Id { get; set; }
    public long InstanceHealthEntityId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsReachable { get; set; }
    public string? State { get; set; }
    public string? ErrorMessage { get; set; }

    public static DatabaseHealthEntity FromModel(DatabaseHealth m) => new()
    {
        DatabaseName = m.DatabaseName,
        IsOnline = m.IsOnline,
        IsReachable = m.IsReachable,
        State = m.State,
        ErrorMessage = m.ErrorMessage,
    };

    public DatabaseHealth ToModel() => new()
    {
        DatabaseName = DatabaseName,
        IsOnline = IsOnline,
        IsReachable = IsReachable,
        State = State,
        ErrorMessage = ErrorMessage,
    };
}
