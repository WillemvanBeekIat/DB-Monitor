using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Services;
using DbMonitor.Infrastructure;
using DbMonitor.Infrastructure.Data;
using DbMonitor.SqlServer.Monitors;
using DbMonitor.Web.Components;
using DbMonitor.Web.Services;
using ApexCharts;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Auto-start Docker PostgreSQL if requested via environment variable
if (Environment.GetEnvironmentVariable("DBMONITOR_AUTO_START_DOCKER") == "true")
{
    await StartDockerPostgresAsync();
}

// Bind only to localhost
builder.WebHost.UseUrls("http://localhost:5000");

// Enable configuration reload for appsettings.json
var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(appSettingsPath))
    appSettingsPath = "appsettings.json";

builder.Configuration.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);

// Get PostgreSQL connection string
var postgresConnectionString = builder.Configuration.GetConnectionString("PostgreSQL");

// Configuration sections
builder.Services.Configure<SqlServerOptions>(builder.Configuration.GetSection(SqlServerOptions.SectionName));
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.SectionName));
builder.Services.Configure<LatencyOptions>(builder.Configuration.GetSection(LatencyOptions.SectionName));
builder.Services.Configure<FragmentationOptions>(builder.Configuration.GetSection(FragmentationOptions.SectionName));
builder.Services.Configure<LongRunningQueryOptions>(builder.Configuration.GetSection(LongRunningQueryOptions.SectionName));
builder.Services.Configure<UsageOptions>(builder.Configuration.GetSection(UsageOptions.SectionName));
builder.Services.Configure<ErrorLogOptions>(builder.Configuration.GetSection(ErrorLogOptions.SectionName));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection(LoggingOptions.SectionName));
builder.Services.Configure<UiOptions>(builder.Configuration.GetSection(UiOptions.SectionName));
builder.Services.Configure<ConfigMutationOptions>(builder.Configuration.GetSection(ConfigMutationOptions.SectionName));

// Core services
builder.Services.AddSingleton<HealthEvaluator>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LatencyOptions>>().Value;
    return new HealthEvaluator(opts);
});
builder.Services.AddSingleton<FragmentationEligibilityEvaluator>();
builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LongRunningQueryOptions>>().Value;
    return new LongRunningQueryEligibilityEvaluator(opts);
});

// Infrastructure (with optional PostgreSQL)
builder.Services.AddInfrastructure(appSettingsPath, postgresConnectionString);

// SQL Server monitors
builder.Services.AddSingleton<IReachabilityMonitor, ReachabilityMonitor>();
builder.Services.AddSingleton<ILatencyMonitor, LatencyMonitor>();
builder.Services.AddSingleton<IFragmentationMonitor, FragmentationMonitor>();
builder.Services.AddSingleton<ILongRunningQueryMonitor, LongRunningQueryMonitor>();
builder.Services.AddSingleton<IUsageMonitor, UsageMonitor>();
builder.Services.AddSingleton<IErrorLogMonitor, ErrorLogMonitor>();
builder.Services.AddSingleton<IActionExecutor, DbMonitor.SqlServer.SqlActionExecutor>();
builder.Services.AddSingleton<IIndexDiscoveryService, DbMonitor.SqlServer.IndexDiscoveryService>();

// UI state service
builder.Services.AddSingleton<MonitoringStateService>();

// Background services
builder.Services.AddHostedService<ReachabilityHostedService>();
builder.Services.AddHostedService<LatencyHostedService>();
builder.Services.AddHostedService<FragmentationHostedService>();
builder.Services.AddHostedService<LongRunningQueryHostedService>();
builder.Services.AddHostedService<ErrorLogHostedService>();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ApexCharts
builder.Services.AddApexCharts();

var app = builder.Build();

// Apply database migrations on startup if PostgreSQL is configured
if (!string.IsNullOrEmpty(postgresConnectionString))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DbMonitorDbContext>>();
    await using var context = await dbContext.CreateDbContextAsync();
    await context.Database.MigrateAsync();
    app.Logger.LogInformation("PostgreSQL database migrations applied successfully");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Helper method to start Docker PostgreSQL container
static async Task StartDockerPostgresAsync()
{
    Console.WriteLine("[DbMonitor] Auto-starting Docker PostgreSQL...");

    // Find solution directory (where docker-compose.yml is located)
    var currentDir = Directory.GetCurrentDirectory();
    var solutionDir = currentDir;

    // Walk up to find docker-compose.yml
    while (!File.Exists(Path.Combine(solutionDir, "docker-compose.yml")))
    {
        var parent = Directory.GetParent(solutionDir);
        if (parent == null)
        {
            Console.WriteLine("[DbMonitor] Warning: docker-compose.yml not found. Skipping Docker auto-start.");
            return;
        }
        solutionDir = parent.FullName;
    }

    try
    {
        // Start docker compose
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "compose up -d",
            WorkingDirectory = solutionDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (!string.IsNullOrEmpty(output))
                Console.WriteLine(output);
            if (!string.IsNullOrEmpty(error) && process.ExitCode != 0)
                Console.WriteLine($"[DbMonitor] Docker error: {error}");
        }

        // Wait for PostgreSQL to be healthy
        Console.WriteLine("[DbMonitor] Waiting for PostgreSQL to be ready...");
        var maxAttempts = 30;
        for (int i = 0; i < maxAttempts; i++)
        {
            var healthInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "inspect --format={{.State.Health.Status}} dbmonitor-postgres",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var healthProcess = Process.Start(healthInfo);
            if (healthProcess != null)
            {
                var health = (await healthProcess.StandardOutput.ReadToEndAsync()).Trim();
                await healthProcess.WaitForExitAsync();

                if (health == "healthy")
                {
                    Console.WriteLine("[DbMonitor] PostgreSQL is ready!");
                    return;
                }
            }

            await Task.Delay(1000);
        }

        Console.WriteLine("[DbMonitor] Warning: PostgreSQL health check timed out. Continuing anyway...");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DbMonitor] Warning: Failed to start Docker: {ex.Message}");
        Console.WriteLine("[DbMonitor] Make sure Docker Desktop is running.");
    }
}
