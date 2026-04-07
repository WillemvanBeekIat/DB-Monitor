using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Models;
using DbMonitor.Core.Services;

namespace DbMonitor.SqlServer.Monitors;

public class LongRunningQueryMonitor : ILongRunningQueryMonitor
{
    private readonly SqlServerOptions _sqlOptions;
    private readonly LongRunningQueryOptions _queryOptions;
    private readonly LongRunningQueryEligibilityEvaluator _evaluator;
    private readonly ILogger<LongRunningQueryMonitor> _logger;

    // sys.dm_exec_requests compatible with SQL Server 2014+
    private const string QuerySql = @"
SELECT
    r.session_id,
    s.login_name,
    s.host_name,
    s.program_name,
    DB_NAME(r.database_id) AS database_name,
    r.command,
    r.start_time,
    DATEDIFF(ms, r.start_time, GETDATE()) AS elapsed_ms,
    r.wait_type,
    r.blocking_session_id,
    SUBSTRING(qt.text, (r.statement_start_offset/2)+1,
        ((CASE r.statement_end_offset
            WHEN -1 THEN DATALENGTH(qt.text)
            ELSE r.statement_end_offset
          END - r.statement_start_offset)/2)+1) AS sql_text
FROM sys.dm_exec_requests r
INNER JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS qt
WHERE r.session_id <> @@SPID
  AND s.is_user_process = 1
  AND DATEDIFF(s, r.start_time, GETDATE()) >= @thresholdSeconds";

    public LongRunningQueryMonitor(
        IOptions<SqlServerOptions> sqlOptions,
        IOptions<LongRunningQueryOptions> queryOptions,
        LongRunningQueryEligibilityEvaluator evaluator,
        ILogger<LongRunningQueryMonitor> logger)
    {
        _sqlOptions = sqlOptions.Value;
        _queryOptions = queryOptions.Value;
        _evaluator = evaluator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LongRunningQuery>> GetLongRunningQueriesAsync(CancellationToken ct = default)
    {
        var result = new List<LongRunningQuery>();

        if (!_queryOptions.Enabled) return result;

        try
        {
            var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
            {
                ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
            };

            using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            using var cmd = new SqlCommand(QuerySql, conn);
            cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("@thresholdSeconds", _queryOptions.ThresholdSeconds);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var startTime = reader.GetDateTime(6);
                var elapsedMs = reader.GetInt32(7);

                var query = new LongRunningQuery
                {
                    SessionId = reader.GetInt16(0),
                    LoginName = reader.IsDBNull(1) ? null : reader.GetString(1),
                    HostName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ProgramName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DatabaseName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Command = reader.IsDBNull(5) ? null : reader.GetString(5),
                    StartTime = new DateTimeOffset(startTime, TimeSpan.Zero),
                    ElapsedDuration = TimeSpan.FromMilliseconds(elapsedMs),
                    WaitType = reader.IsDBNull(8) ? null : reader.GetString(8),
                    BlockingSessionId = reader.IsDBNull(9) ? null : (int?)reader.GetInt16(9),
                    SqlText = reader.IsDBNull(10) ? null : reader.GetString(10)
                };

                var (eligible, reason) = _evaluator.Evaluate(query);
                query.WouldBeKilled = eligible && _queryOptions.KillEnabled;
                query.SkipReason = eligible ? null : reason;

                result.Add(query);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Long-running query check failed");
        }

        return result;
    }
}
