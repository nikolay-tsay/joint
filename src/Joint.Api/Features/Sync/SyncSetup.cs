using Hangfire;
using Joint.Api.Connectors;

namespace Joint.Api.Features.Sync;

public static class SyncSetup
{
    public static IServiceCollection AddSync(this IServiceCollection services)
    {
        services.AddSingleton<ConnectorRegistry>();
        services.AddScoped<SyncJob>();

        return services;
    }

    public static void ScheduleSync(this IHost app) =>
        app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<SyncJob>(
            "sync-all",
            job => job.RunAsync(CancellationToken.None),
            Cron.Hourly());
}
