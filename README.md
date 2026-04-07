# DB Monitor

A lightweight, production-style SQL Server 2014+ monitoring application built with .NET 8 Blazor Server. Runs locally, monitors one SQL Server instance with low overhead, and shows health information in a modern responsive dashboard.

---

## Architecture

```
DbMonitor.slnx
├── src/
│   ├── DbMonitor.Core           # Domain models, interfaces, config, business logic
│   ├── DbMonitor.SqlServer      # SQL Server monitors + action executor
│   ├── DbMonitor.Infrastructure # File-based persistence, log reader, audit writer
│   └── DbMonitor.Web            # Blazor Web App (UI + background hosted services)
└── tests/
    └── DbMonitor.Tests          # xUnit unit tests (45 tests)
```

### Why Blazor Interactive Server?

- **Real-time updates** — SignalR connection enables push-based UI updates from background services without JavaScript polling.
- **No API layer** — Background services share in-memory state (`MonitoringStateService`) directly with Razor components.
- **Single deployment unit** — One process, one port. Perfect for a local monitoring tool.
- **localhost-only** — No CORS, no auth configuration needed for version 1.
- **Progressive path** — Can be moved behind a reverse proxy or IIS with authentication added later.

---

## Project Structure

| Project | Responsibility |
|---|---|
| `DbMonitor.Core` | Models (`InstanceHealth`, `IndexInfo`, `LongRunningQuery`, etc.), all configuration option classes, interfaces, `HealthEvaluator`, `FragmentationEligibilityEvaluator`, `LongRunningQueryEligibilityEvaluator` |
| `DbMonitor.SqlServer` | `ReachabilityMonitor`, `LatencyMonitor`, `FragmentationMonitor`, `LongRunningQueryMonitor`, `UsageMonitor`, `ErrorLogMonitor`, `SqlActionExecutor` |
| `DbMonitor.Infrastructure` | `JsonStateStore`, `JsonAuditWriter`, `JsonLogFileReader`, `StructuredLogWriter`, `ConfigWriter`, `NullNotificationPublisher`, `HealthAggregator`, DI registration |
| `DbMonitor.Web` | Blazor pages (Dashboard, Long Queries, Indexes, Errors, Audit Trail, Logs, Settings), background hosted services, `MonitoringStateService` |
| `DbMonitor.Tests` | Unit tests for evaluators, state store, audit writer, config writer, log parser |

---

## How to Run Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- A SQL Server instance (2014 or newer)
- SQL permissions listed in the [SQL Permissions](#sql-permissions) section

### Step 1 — Configure the connection string

Edit `src/DbMonitor.Web/appsettings.json`:

```json
{
  "SqlServer": {
    "ConnectionString": "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

### Step 2 — Configure monitored indexes (optional)

```json
"Fragmentation": {
  "MonitoredIndexes": [
    {
      "Database": "MyDatabase",
      "Schema": "dbo",
      "Table": "Orders",
      "Index": "IX_Orders_OrderDate",
      "Enabled": true,
      "FragmentationPercentThreshold": 30.0,
      "MinimumPageCount": 1000,
      "AllowReorganize": true
    }
  ]
}
```

### Step 3 — Run

```bash
cd src/DbMonitor.Web
dotnet run
```

Open your browser to **http://localhost:5000**

---

## How Localhost-Only Hosting is Configured

In `Program.cs`:

```csharp
builder.WebHost.UseUrls("http://localhost:5000");
```

No external network access is possible in version 1.

---

## How to Publish

```bash
cd src/DbMonitor.Web
dotnet publish -c Release -o ./publish
cd ./publish
dotnet DbMonitor.Web.dll
```

---

## How Dry Run Works

Every destructive action respects a `DryRun` flag:

- **Fragmentation.DryRun** — when `true`, no `ALTER INDEX ... REORGANIZE` is executed. The action is logged as `DryRun`.
- **LongRunningQueries.DryRun** — when `true`, no session is terminated. The action is logged as `DryRun`.

Dry run is **on by default**. Monitoring and detection still happen; only the SQL command is suppressed.

Toggle dry run from the **Settings** page. The change is written back to `appsettings.json` immediately and persists across restarts.

---

## How Manual Actions Work

### Manual Query Cancellation
1. Go to **Long Queries** page.
2. Click **Kill** on a session — a confirmation dialog shows session details.
3. Manual actions **override protection rules** (AllowedLogins, AllowedHosts, etc.).
4. Confirm to execute. The action is logged with `Trigger = Manual, IsManualOverride = true`.

### Manual Index Reorganize
1. Go to **Indexes** page.
2. Click **Reorganize** on a configured index.
3. Only indexes listed in `MonitoredIndexes` can be reorganized from the UI.
4. The action is logged to the audit trail.

---

## How Audit Logging Works

Every action (automatic or manual, dry-run or real) is written to:

```
data/audit/audit-YYYY-MM-DD.jsonl
```

Fields: `Id`, `Timestamp`, `ActionType`, `Trigger`, `Target`, `Reason`, `IsDryRun`, `Outcome`, `IsManualOverride`, `TriggeredBy`, `ErrorMessage`.

View the full audit trail in the **Audit Trail** page.

---

## How Log / History Files Work

Structured JSON events are written to:

```
logs/dbmonitor-YYYY-MM-DD.jsonl
```

Each line is a Serilog CLEF-compatible JSON object:

```json
{"@t":"2024-01-15T10:00:00+00:00","@mt":"Instance reachable","@l":"Information","EventType":"ReachabilityCheck"}
```

### Opening an Older Log File

1. Navigate to the **Logs** page.
2. Use the dropdown to select a file from the `logs/` directory.
3. Click **Load** to parse and display structured events.
4. Filter events with the search box.

---

## SQL Permissions

```sql
-- Server-level (required)
GRANT VIEW SERVER STATE TO [db_monitor];
GRANT VIEW ANY DATABASE TO [db_monitor];
GRANT EXECUTE ON xp_readerrorlog TO [db_monitor];

-- Per monitored database (for fragmentation checks)
-- GRANT VIEW DATABASE STATE TO [db_monitor];

-- Only needed when DryRun = false and KillEnabled = true
-- GRANT ALTER ANY CONNECTION TO [db_monitor];

-- Only needed when DryRun = false for index reorganize
-- GRANT ALTER ON SCHEMA::dbo TO [db_monitor];
```

---

## How to Later Add Webhook Notifications

The `INotificationPublisher` interface is already in place:

1. Create `WebhookNotificationPublisher : INotificationPublisher` in `DbMonitor.Infrastructure/Notifications/`.
2. POST to `NotificationOptions.WebhookUrl` in `PublishAsync`.
3. Change DI registration in `ServiceCollectionExtensions.cs`.
4. Set `Notifications.Enabled = true` and `UseWebhook = true` in `appsettings.json`.

---

## How to Later Add Authentication

1. Add `Microsoft.AspNetCore.Authentication` packages.
2. Add `app.UseAuthentication(); app.UseAuthorization();` in `Program.cs`.
3. Add `[Authorize]` to Blazor pages.
4. Configure identity provider in `appsettings.json`.

---

## Running Tests

```bash
dotnet test tests/DbMonitor.Tests
```

---

## Assumptions

1. SQL Server is accessible from the monitoring machine.
2. All SQL queries use APIs available in SQL Server 2014 (DMVs, `xp_readerrorlog`).
3. Monitored indexes must be explicitly configured — no automatic discovery.
4. Dry run is always on by default.
5. No authentication is needed in version 1 (localhost-only).

## Recommended Default Intervals

| Monitor | Default | Rationale |
|---|---|---|
| Instance reachability | 30 s | Fast outage detection |
| Latency | 30 s | Trend tracking |
| Long-running queries | 15 s | Quick blocking detection |
| Error log | 60 s | SQL log grows slowly |
| Fragmentation | 60 min | Expensive DMV; fragmentation changes slowly |
| Usage snapshots | 30 min | Low overhead |

## Version 1 Limitations

- No authentication — localhost only
- No database-backed history — files only
- Settings are read-only except the dry-run toggle
- No webhook notifications (interface ready, not wired)
- No multi-instance support
- Charts use CSS bars, not a JavaScript chart library

## Future Extensions

| Extension | Path |
|---|---|
| Webhook notifications | Implement `INotificationPublisher` |
| Authentication | Add `[Authorize]` + ASP.NET Core Identity or OIDC |
| Server deployment | Publish + IIS / nginx reverse proxy |
| Database-backed history | Replace file stores with EF Core |
| NoSQL log storage | Replace `ILogFileReader` with MongoDB/Elasticsearch |
| Multiple SQL instances | Extend `SqlServerOptions` to a list |
| Editable settings | Extend `IConfigWriter` to patch any JSON section |
| Windows Service | Add `UseWindowsService()` in `Program.cs` |