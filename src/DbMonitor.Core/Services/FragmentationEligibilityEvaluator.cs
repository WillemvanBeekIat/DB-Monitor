using System;
using System.Collections.Generic;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Services;

public class FragmentationEligibilityEvaluator
{
    public (bool Eligible, string? Reason) Evaluate(IndexInfo index, MonitoredIndexConfig config, DateTimeOffset? lastCooldown, int cooldownMinutes)
    {
        if (!config.Enabled)
            return (false, "Index monitoring is disabled in configuration");

        if (!config.AllowReorganize)
            return (false, "AllowReorganize is false for this index");

        if (index.FragmentationPercent < config.FragmentationPercentThreshold)
            return (false, $"Fragmentation {index.FragmentationPercent:F1}% below threshold {config.FragmentationPercentThreshold}%");

        if (index.PageCount < config.MinimumPageCount)
            return (false, $"Page count {index.PageCount} below minimum {config.MinimumPageCount}");

        if (index.UsageScore < config.MinimumUsageScore)
            return (false, $"Usage score {index.UsageScore:F2} below minimum {config.MinimumUsageScore}");

        if (lastCooldown.HasValue && lastCooldown.Value > DateTimeOffset.UtcNow)
            return (false, $"In cooldown until {lastCooldown.Value:u}");

        return (true, null);
    }

    public double CalculateUsageScore(long seeks, long scans, long lookups, long updates, IndexUsageWeights weights)
    {
        return (seeks * weights.SeekWeight)
             + (scans * weights.ScanWeight)
             + (lookups * weights.LookupWeight)
             + (updates * weights.UpdateWeight);
    }
}
