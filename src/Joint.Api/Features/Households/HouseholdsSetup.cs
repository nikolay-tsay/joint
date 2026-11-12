using Hangfire;

namespace Joint.Api.Features.Households;

public static class HouseholdsSetup
{
    public static IServiceCollection AddHouseholds(this IServiceCollection services) =>
        services.AddScoped<PurgeInvitationsJob>();

    /// <summary>Runs the invitation purge once a day, at 03:00 UTC.</summary>
    public static void ScheduleInvitationPurge(this IHost app) =>
        app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<PurgeInvitationsJob>(
            "purge-invitations",
            job => job.RunAsync(CancellationToken.None),
            Cron.Daily(3));
}
