using DbMonitor.Core.Configuration;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Services;

public class HealthEvaluator
{
    private readonly LatencyOptions _latency;

    public HealthEvaluator(LatencyOptions latency)
    {
        _latency = latency;
    }

    public HealthStatus EvaluateLatency(double ms)
    {
        if (ms >= _latency.CriticalMs) return HealthStatus.Critical;
        if (ms >= _latency.WarningMs) return HealthStatus.Warning;
        return HealthStatus.Healthy;
    }

    public HealthStatus EvaluateInstance(InstanceHealth health)
    {
        if (!health.IsReachable) return HealthStatus.Critical;
        if (health.ConsecutiveFailures > 0) return HealthStatus.Warning;
        if (health.ProbeQueryMs.HasValue)
            return EvaluateLatency(health.ProbeQueryMs.Value);
        return HealthStatus.Healthy;
    }
}
