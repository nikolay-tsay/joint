using Hangfire;
using Hangfire.PostgreSql;

namespace Joint.Api.Common;

public static class BackgroundJobs
{
    /// <summary>
    /// Hangfire, storing its state in the same database. Features register and schedule
    /// their own jobs; this only provides the machinery.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, string connectionString)
    {
        services.AddHangfire(config => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

        return services;
    }
}
