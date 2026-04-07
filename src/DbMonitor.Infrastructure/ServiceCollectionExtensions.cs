using DbMonitor.Core.Interfaces;
using DbMonitor.Infrastructure.Data;
using DbMonitor.Infrastructure.Logging;
using DbMonitor.Infrastructure.Notifications;
using DbMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DbMonitor.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string appSettingsPath, string? postgresConnectionString = null)
    {
        if (!string.IsNullOrEmpty(postgresConnectionString))
        {
            // Use PostgreSQL database for state and audit
            services.AddDbContextFactory<DbMonitorDbContext>(options =>
                options.UseNpgsql(postgresConnectionString));

            services.AddScoped<IStateStore, EfStateStore>();
            services.AddScoped<IAuditWriter, EfAuditWriter>();
        }
        else
        {
            // Fall back to JSON file storage
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
        }

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
