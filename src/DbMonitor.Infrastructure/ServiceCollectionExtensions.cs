using DbMonitor.Core.Interfaces;
using DbMonitor.Infrastructure.Logging;
using DbMonitor.Infrastructure.Notifications;
using DbMonitor.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string appSettingsPath)
    {
        services.AddSingleton<IStateStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<JsonStateStore>>();
            return new JsonStateStore("data/state", logger);
        });

        services.AddSingleton<IAuditWriter>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<JsonAuditWriter>>();
            return new JsonAuditWriter("data/audit", logger);
        });

        services.AddSingleton<ILogFileReader, JsonLogFileReader>();
        services.AddSingleton<StructuredLogWriter>();

        services.AddSingleton<INotificationPublisher, NullNotificationPublisher>();

        services.AddSingleton<IConfigWriter>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConfigWriter>>();
            return new ConfigWriter(appSettingsPath, logger);
        });

        services.AddSingleton<IHealthAggregator, HealthAggregator>();

        return services;
    }
}
