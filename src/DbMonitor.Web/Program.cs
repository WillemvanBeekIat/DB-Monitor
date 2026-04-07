using DbMonitor.Core.Configuration;
using DbMonitor.Core.Interfaces;
using DbMonitor.Core.Services;
using DbMonitor.Infrastructure;
using DbMonitor.SqlServer.Monitors;
using DbMonitor.Web.Components;
using DbMonitor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Bind only to localhost
builder.WebHost.UseUrls("http://localhost:5000");

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

// Infrastructure
var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(appSettingsPath))
    appSettingsPath = "appsettings.json";

builder.Services.AddInfrastructure(appSettingsPath);

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

var app = builder.Build();

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
