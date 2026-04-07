using System;
using DbMonitor.Core.Models;

namespace DbMonitor.Infrastructure.Data;

public class AuditEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; }
    public ActionType ActionType { get; set; }
    public ActionTrigger Trigger { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsDryRun { get; set; }
    public ActionOutcome Outcome { get; set; }
    public bool IsManualOverride { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredBy { get; set; }

    public static AuditEntryEntity FromModel(AuditEntry m) => new()
    {
        Id = m.Id,
        Timestamp = m.Timestamp,
        ActionType = m.ActionType,
        Trigger = m.Trigger,
        Target = m.Target,
        Reason = m.Reason,
        IsDryRun = m.IsDryRun,
        Outcome = m.Outcome,
        IsManualOverride = m.IsManualOverride,
        ErrorMessage = m.ErrorMessage,
        TriggeredBy = m.TriggeredBy,
    };

    public AuditEntry ToModel() => new()
    {
        Id = Id,
        Timestamp = Timestamp,
        ActionType = ActionType,
        Trigger = Trigger,
        Target = Target,
        Reason = Reason,
        IsDryRun = IsDryRun,
        Outcome = Outcome,
        IsManualOverride = IsManualOverride,
        ErrorMessage = ErrorMessage,
        TriggeredBy = TriggeredBy,
    };
}
