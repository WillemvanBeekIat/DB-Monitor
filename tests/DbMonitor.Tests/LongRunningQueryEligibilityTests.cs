using System;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Models;
using DbMonitor.Core.Services;
using Xunit;

namespace DbMonitor.Tests;

public class LongRunningQueryEligibilityTests
{
    private static LongRunningQueryEligibilityEvaluator Sut(LongRunningQueryOptions? opts = null) =>
        new(opts ?? DefaultOptions());

    private static LongRunningQueryOptions DefaultOptions() => new()
    {
        ThresholdSeconds = 300,
        IgnoreSqlAgentJobs = true,
        IgnoreBackups = true,
        IgnoreRestores = true,
        IgnoreCheckDb = true,
        IgnoreIndexMaintenance = true,
        AllowedLogins = new() { "sa", "admin" },
        AllowedHosts = new() { "BACKUP-SERVER" },
        AllowedPrograms = new() { "SqlBackup" },
        IgnoredCommands = new() { "CHECKPOINT" }
    };

    private static LongRunningQuery LongQuery(double elapsedSeconds = 400) => new()
    {
        SessionId = 55,
        LoginName = "appuser",
        HostName = "APP01",
        ProgramName = "MyApp",
        DatabaseName = "AppDb",
        Command = "SELECT",
        ElapsedDuration = TimeSpan.FromSeconds(elapsedSeconds)
    };

    [Fact]
    public void Eligible_WhenAboveThreshold()
    {
        var (eligible, _) = Sut().Evaluate(LongQuery(400));
        Assert.True(eligible);
    }

    [Fact]
    public void NotEligible_WhenBelowThreshold()
    {
        var (eligible, _) = Sut().Evaluate(LongQuery(200));
        Assert.False(eligible);
    }

    [Fact]
    public void NotEligible_WhenLoginIsAllowed()
    {
        var q = LongQuery();
        q.LoginName = "sa"; // in AllowedLogins
        var (eligible, reason) = Sut().Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("AllowedLogins", reason);
    }

    [Fact]
    public void NotEligible_WhenHostIsAllowed()
    {
        var q = LongQuery();
        q.HostName = "BACKUP-SERVER";
        var (eligible, reason) = Sut().Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("AllowedHosts", reason);
    }

    [Fact]
    public void NotEligible_WhenProgramIsAllowed()
    {
        var q = LongQuery();
        q.ProgramName = "SqlBackup";
        var (eligible, reason) = Sut().Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("AllowedPrograms", reason);
    }

    [Fact]
    public void NotEligible_WhenIsSqlAgentJob()
    {
        var q = LongQuery();
        q.ProgramName = "SQLAgent - TSQL JobStep";
        var (eligible, reason) = Sut().Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("SQL Agent", reason);
    }

    [Fact]
    public void NotEligible_WhenIsBackup()
    {
        var q = LongQuery();
        q.Command = "BACKUP DATABASE";
        var (eligible, reason) = Sut().Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("Backup", reason);
    }

    [Fact]
    public void ManualOverride_IgnoresProtectionRules()
    {
        var q = LongQuery(400);
        q.LoginName = "sa"; // normally protected
        var (eligible, _) = Sut().Evaluate(q, manualOverride: true);
        Assert.True(eligible);
    }

    [Fact]
    public void NotEligible_WhenCommandIgnored()
    {
        var q = LongQuery();
        q.Command = "CHECKPOINT";
        var (eligible, reason) = Sut().Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("IgnoredCommands", reason);
    }

    [Fact]
    public void NotEligible_WhenDatabaseExcluded()
    {
        var opts = DefaultOptions();
        opts.ExcludedDatabases.Add("AppDb");
        var q = LongQuery();
        var (eligible, reason) = Sut(opts).Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("excluded", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotEligible_WhenDatabaseNotInIncluded()
    {
        var opts = DefaultOptions();
        opts.IncludedDatabases.Add("OtherDb"); // AppDb not in list
        var q = LongQuery();
        var (eligible, reason) = Sut(opts).Evaluate(q);
        Assert.False(eligible);
        Assert.Contains("IncludedDatabases", reason);
    }
}
