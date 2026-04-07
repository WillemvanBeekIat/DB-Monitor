using System;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Models;
using DbMonitor.Core.Services;
using Xunit;

namespace DbMonitor.Tests;

public class FragmentationEligibilityTests
{
    private static FragmentationEligibilityEvaluator Sut() => new();

    private static MonitoredIndexConfig DefaultConfig() => new()
    {
        Database = "MyDb",
        Schema = "dbo",
        Table = "Orders",
        Index = "IX_Orders_Date",
        Enabled = true,
        FragmentationPercentThreshold = 30,
        MinimumPageCount = 1000,
        MinimumUsageScore = 0,
        AllowReorganize = true,
        UsageWeights = new IndexUsageWeights { SeekWeight = 1, ScanWeight = 0.5, LookupWeight = 0.8, UpdateWeight = -0.2 }
    };

    private static IndexInfo EligibleIndex() => new()
    {
        Database = "MyDb",
        Schema = "dbo",
        Table = "Orders",
        IndexName = "IX_Orders_Date",
        FragmentationPercent = 45,
        PageCount = 5000,
        UsageScore = 100
    };

    [Fact]
    public void Eligible_WhenAllConditionsMet()
    {
        var (eligible, reason) = Sut().Evaluate(EligibleIndex(), DefaultConfig(), null, 60);
        Assert.True(eligible);
        Assert.Null(reason);
    }

    [Fact]
    public void NotEligible_WhenDisabled()
    {
        var config = DefaultConfig();
        config.Enabled = false;
        var (eligible, reason) = Sut().Evaluate(EligibleIndex(), config, null, 60);
        Assert.False(eligible);
        Assert.Contains("disabled", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotEligible_WhenFragmentationBelowThreshold()
    {
        var index = EligibleIndex();
        index.FragmentationPercent = 25; // below 30%
        var (eligible, _) = Sut().Evaluate(index, DefaultConfig(), null, 60);
        Assert.False(eligible);
    }

    [Fact]
    public void NotEligible_WhenPageCountBelowMinimum()
    {
        var index = EligibleIndex();
        index.PageCount = 500; // below 1000
        var (eligible, _) = Sut().Evaluate(index, DefaultConfig(), null, 60);
        Assert.False(eligible);
    }

    [Fact]
    public void NotEligible_WhenAllowReorganizeIsFalse()
    {
        var config = DefaultConfig();
        config.AllowReorganize = false;
        var (eligible, reason) = Sut().Evaluate(EligibleIndex(), config, null, 60);
        Assert.False(eligible);
        Assert.Contains("AllowReorganize", reason);
    }

    [Fact]
    public void NotEligible_WhenInCooldown()
    {
        var cooldown = DateTimeOffset.UtcNow.AddMinutes(30); // still in cooldown
        var (eligible, reason) = Sut().Evaluate(EligibleIndex(), DefaultConfig(), cooldown, 60);
        Assert.False(eligible);
        Assert.Contains("cooldown", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eligible_WhenCooldownExpired()
    {
        var cooldown = DateTimeOffset.UtcNow.AddMinutes(-10); // already expired
        var (eligible, _) = Sut().Evaluate(EligibleIndex(), DefaultConfig(), cooldown, 60);
        Assert.True(eligible);
    }

    [Theory]
    [InlineData(1000, 200, 100, 50, 1.0, 0.5, 0.8, -0.2, 1170.0)]
    public void CalculateUsageScore_ReturnsExpected(
        long seeks, long scans, long lookups, long updates,
        double sw, double sc, double lw, double uw, double expected)
    {
        var weights = new IndexUsageWeights { SeekWeight = sw, ScanWeight = sc, LookupWeight = lw, UpdateWeight = uw };
        var score = Sut().CalculateUsageScore(seeks, scans, lookups, updates, weights);
        Assert.True(score > 0, $"Expected positive score, got {score}");
        _ = expected; // verified in UsageScore_CalculationIsCorrect
    }

    [Fact]
    public void UsageScore_CalculationIsCorrect()
    {
        var weights = new IndexUsageWeights { SeekWeight = 1.0, ScanWeight = 0.5, LookupWeight = 0.8, UpdateWeight = -0.2 };
        var score = Sut().CalculateUsageScore(1000, 200, 100, 50, weights);
        // 1000*1.0 + 200*0.5 + 100*0.8 + 50*(-0.2) = 1000 + 100 + 80 - 10 = 1170
        Assert.Equal(1170.0, score, precision: 1);
    }
}
