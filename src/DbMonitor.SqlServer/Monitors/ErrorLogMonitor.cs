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

namespace DbMonitor.SqlServer.Monitors;

public class ErrorLogMonitor : IErrorLogMonitor
{
    private readonly SqlServerOptions _sqlOptions;
    private readonly ErrorLogOptions _errorOptions;
    private readonly IStateStore _stateStore;
    private readonly ILogger<ErrorLogMonitor> _logger;

    public ErrorLogMonitor(
        IOptions<SqlServerOptions> sqlOptions,
        IOptions<ErrorLogOptions> errorOptions,
        IStateStore stateStore,
        ILogger<ErrorLogMonitor> logger)
    {
        _sqlOptions = sqlOptions.Value;
        _errorOptions = errorOptions.Value;
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SqlErrorEntry>> ReadNewErrorsAsync(CancellationToken ct = default)
    {
        var result = new List<SqlErrorEntry>();

        if (!_errorOptions.Enabled) return result;

        try
        {
            var bookmark = await _stateStore.LoadErrorBookmarkAsync(ct);
            DateTimeOffset? since = null;
            if (bookmark != null && DateTimeOffset.TryParse(bookmark, out var parsedTs))
                since = parsedTs;

            var csb = new SqlConnectionStringBuilder(_sqlOptions.ConnectionString)
            {
                ConnectTimeout = _sqlOptions.ConnectionTimeoutSeconds
            };

            using var conn = new SqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            // xp_readerrorlog: supported on SQL Server 2014+
            using var cmd = new SqlCommand("EXEC xp_readerrorlog 0, 1, NULL, NULL, @startTime, NULL, 'asc'", conn);
            cmd.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
            cmd.Parameters.AddWithValue("@startTime",
                since.HasValue ? (object)since.Value.LocalDateTime : DBNull.Value);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            DateTimeOffset? latestTs = null;

            while (await reader.ReadAsync(ct))
            {
                var logDate = reader.IsDBNull(0) ? DateTime.Now : reader.GetDateTime(0);
                var message = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                if (string.IsNullOrWhiteSpace(message)) continue;

                var entryTs = new DateTimeOffset(logDate, TimeZoneInfo.Local.GetUtcOffset(logDate));

                if (since.HasValue && entryTs <= since.Value) continue;

                if (!IsRelevant(message)) continue;

                var entry = new SqlErrorEntry
                {
                    Timestamp = entryTs,
                    Message = message,
                    Category = ClassifyMessage(message)
                };

                result.Add(entry);

                if (!latestTs.HasValue || entryTs > latestTs.Value)
                    latestTs = entryTs;
            }

            if (latestTs.HasValue)
                await _stateStore.SaveErrorBookmarkAsync(latestTs.Value.ToString("o"), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error log reading failed");
        }

        return result;
    }

    private bool IsRelevant(string message)
    {
        foreach (var keyword in _errorOptions.Keywords)
        {
            if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static SqlErrorCategory ClassifyMessage(string message)
    {
        if (message.Contains("deadlock", StringComparison.OrdinalIgnoreCase))
            return SqlErrorCategory.Deadlock;
        if (message.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("consistency", StringComparison.OrdinalIgnoreCase))
            return SqlErrorCategory.Corruption;
        if (message.Contains("I/O", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("disk", StringComparison.OrdinalIgnoreCase))
            return SqlErrorCategory.IO;
        if (message.Contains("memory", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("resource", StringComparison.OrdinalIgnoreCase))
            return SqlErrorCategory.Resource;
        if (message.Contains("severity", StringComparison.OrdinalIgnoreCase))
            return SqlErrorCategory.Severe;
        return SqlErrorCategory.Other;
    }
}
