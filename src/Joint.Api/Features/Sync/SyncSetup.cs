using Hangfire;
using Joint.Api.Common.Security;
using Joint.Api.Connectors;
using Joint.Api.Connectors.Fio;
using Joint.Api.Connectors.LunchFlow;
using Joint.Api.Connectors.Trading212;

namespace Joint.Api.Features.Sync;

public static class SyncSetup
{
    public static IServiceCollection AddSync(this IServiceCollection services, IConfiguration configuration)
    {
        // Created on first use, so a missing key fails the request that needs it, not startup.
        services.AddSingleton(_ => CredentialProtector.FromConfiguration(configuration));

        services.AddHttpClient<LunchFlowConnector>(http => http.BaseAddress = new Uri(LunchFlowConnector.BaseUrl));
        services.AddScoped<IProviderConnector>(provider => provider.GetRequiredService<LunchFlowConnector>());

        services.AddHttpClient<Trading212Connector>(http => http.BaseAddress = new Uri(Trading212Connector.BaseUrl));
        services.AddScoped<IProviderConnector>(provider => provider.GetRequiredService<Trading212Connector>());

        // Fio carries the token in the URL, and the default HTTP client loggers write URLs out.
        services.AddHttpClient<FioConnector>(http => http.BaseAddress = new Uri(FioConnector.BaseUrl))
            .RemoveAllLoggers();
        services.AddScoped<IProviderConnector>(provider => provider.GetRequiredService<FioConnector>());

        services.AddScoped<ConnectorRegistry>();
        services.AddScoped<ConnectionSyncer>();
        services.AddScoped<Joint.Api.Features.Connections.ConnectionLinker>();
        services.AddScoped<SyncJob>();

        return services;
    }

    /// <summary>
    /// Every six hours. Lunch Flow refreshes about once a day, and banks commonly allow only
    /// a handful of unattended reads a day, so a tighter schedule would only burn quota.
    /// </summary>
    public static void ScheduleSync(this IHost app) =>
        app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<SyncJob>(
            "sync-all",
            job => job.RunAsync(CancellationToken.None),
            "0 */6 * * *");
}
