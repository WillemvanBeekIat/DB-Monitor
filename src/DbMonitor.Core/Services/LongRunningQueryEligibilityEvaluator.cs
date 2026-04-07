using System;
using System.Linq;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Models;

namespace DbMonitor.Core.Services;

public class LongRunningQueryEligibilityEvaluator
{
    private readonly LongRunningQueryOptions _options;

    public LongRunningQueryEligibilityEvaluator(LongRunningQueryOptions options)
    {
        _options = options;
    }

    public (bool Eligible, string Reason) Evaluate(LongRunningQuery query, bool manualOverride = false)
    {
        if (!manualOverride)
        {
            if (_options.AllowedLogins.Any(l => string.Equals(l, query.LoginName, StringComparison.OrdinalIgnoreCase)))
                return (false, $"Login '{query.LoginName}' is in AllowedLogins");

            if (_options.AllowedHosts.Any(h => string.Equals(h, query.HostName, StringComparison.OrdinalIgnoreCase)))
                return (false, $"Host '{query.HostName}' is in AllowedHosts");

            if (_options.AllowedPrograms.Any(p => string.Equals(p, query.ProgramName, StringComparison.OrdinalIgnoreCase)))
                return (false, $"Program '{query.ProgramName}' is in AllowedPrograms");

            if (!string.IsNullOrEmpty(query.Command) &&
                _options.IgnoredCommands.Any(c => string.Equals(c, query.Command, StringComparison.OrdinalIgnoreCase)))
                return (false, $"Command '{query.Command}' is in IgnoredCommands");

            if (_options.IgnoreSqlAgentJobs && IsSqlAgentJob(query))
                return (false, "SQL Agent job – protected by IgnoreSqlAgentJobs");

            if (_options.IgnoreBackups && IsBackup(query))
                return (false, "Backup operation – protected by IgnoreBackups");

            if (_options.IgnoreRestores && IsRestore(query))
                return (false, "Restore operation – protected by IgnoreRestores");

            if (_options.IgnoreCheckDb && IsCheckDb(query))
                return (false, "CHECKDB – protected by IgnoreCheckDb");

            if (_options.IgnoreIndexMaintenance && IsIndexMaintenance(query))
                return (false, "Index maintenance – protected by IgnoreIndexMaintenance");

            if (_options.ExcludedDatabases.Any(d => string.Equals(d, query.DatabaseName, StringComparison.OrdinalIgnoreCase)))
                return (false, $"Database '{query.DatabaseName}' is excluded");

            if (_options.IncludedDatabases.Count > 0 &&
                !_options.IncludedDatabases.Any(d => string.Equals(d, query.DatabaseName, StringComparison.OrdinalIgnoreCase)))
                return (false, $"Database '{query.DatabaseName}' is not in IncludedDatabases");
        }

        if (query.ElapsedDuration.TotalSeconds < _options.ThresholdSeconds)
            return (false, $"Elapsed {query.ElapsedDuration.TotalSeconds:F0}s below threshold {_options.ThresholdSeconds}s");

        return (true, "Eligible for cancellation");
    }

    private static bool IsSqlAgentJob(LongRunningQuery q) =>
        string.Equals(q.ProgramName, "SQLAgent - TSQL JobStep", StringComparison.OrdinalIgnoreCase) ||
        (q.ProgramName?.StartsWith("SQLAgent", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsBackup(LongRunningQuery q) =>
        q.Command?.Contains("BACKUP", StringComparison.OrdinalIgnoreCase) ?? false;

    private static bool IsRestore(LongRunningQuery q) =>
        q.Command?.Contains("RESTORE", StringComparison.OrdinalIgnoreCase) ?? false;

    private static bool IsCheckDb(LongRunningQuery q) =>
        q.Command?.Contains("DBCC", StringComparison.OrdinalIgnoreCase) ?? false;

    private static bool IsIndexMaintenance(LongRunningQuery q) =>
        (q.Command?.Contains("ALTER INDEX", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (q.SqlText?.Contains("ALTER INDEX", StringComparison.OrdinalIgnoreCase) ?? false);
}
