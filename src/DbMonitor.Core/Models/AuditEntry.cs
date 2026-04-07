using System;

namespace DbMonitor.Core.Models;

public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public ActionType ActionType { get; set; }
    public ActionTrigger Trigger { get; set; }
    public string Target { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsDryRun { get; set; }
    public ActionOutcome Outcome { get; set; }
    public bool IsManualOverride { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredBy { get; set; }
}
