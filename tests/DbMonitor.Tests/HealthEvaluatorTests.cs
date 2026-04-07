using DbMonitor.Core.Configuration;
using DbMonitor.Core.Models;
using DbMonitor.Core.Services;
using Xunit;

namespace DbMonitor.Tests;

public class HealthEvaluatorTests
{
    private static HealthEvaluator Create(double warning = 500, double critical = 2000) =>
        new(new LatencyOptions { WarningMs = warning, CriticalMs = critical });

    [Theory]
    [InlineData(100, HealthStatus.Healthy)]
    [InlineData(499, HealthStatus.Healthy)]
    [InlineData(500, HealthStatus.Warning)]
    [InlineData(1000, HealthStatus.Warning)]
    [InlineData(2000, HealthStatus.Critical)]
    [InlineData(5000, HealthStatus.Critical)]
    public void EvaluateLatency_ReturnsExpectedStatus(double ms, HealthStatus expected)
    {
        var sut = Create();
        Assert.Equal(expected, sut.EvaluateLatency(ms));
    }

    [Fact]
    public void EvaluateInstance_NotReachable_ReturnsCritical()
    {
        var sut = Create();
        var health = new InstanceHealth { IsReachable = false };
        Assert.Equal(HealthStatus.Critical, sut.EvaluateInstance(health));
    }

    [Fact]
    public void EvaluateInstance_Reachable_FastProbe_ReturnsHealthy()
    {
        var sut = Create();
        var health = new InstanceHealth { IsReachable = true, ProbeQueryMs = 100 };
        Assert.Equal(HealthStatus.Healthy, sut.EvaluateInstance(health));
    }

    [Fact]
    public void EvaluateInstance_Reachable_SlowProbe_ReturnsWarning()
    {
        var sut = Create();
        var health = new InstanceHealth { IsReachable = true, ProbeQueryMs = 800 };
        Assert.Equal(HealthStatus.Warning, sut.EvaluateInstance(health));
    }
}
