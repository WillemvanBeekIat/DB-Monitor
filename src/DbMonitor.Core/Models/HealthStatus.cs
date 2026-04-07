namespace DbMonitor.Core.Models;

public enum HealthStatus { Healthy, Warning, Critical, Unknown }
public enum ActionTrigger { Automatic, Manual }
public enum ActionType { ReorganizeIndex, KillQuery }
public enum ActionOutcome { DryRun, Executed, Skipped, Failed }
