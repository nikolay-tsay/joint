using System.Security.Claims;
using Hangfire;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Features.Sync;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Connections;

/// <summary>
/// Starts a sync of one of the household's connections right away, without waiting for
/// the schedule. A development aid: it is not mapped in Production, where nothing limits
/// how often it could be called and providers allow only a few reads a day.
/// </summary>
public class SyncConnection : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/connections/{connectionId:guid}/sync", HandleAsync)
            .WithName("SyncConnection")
            .RequireAuthorization();

    public static async Task<Results<Accepted, UnauthorizedHttpResult, NotFound>> HandleAsync(
        Guid connectionId,
        ClaimsPrincipal principal,
        CurrentUserService currentUser,
        IBackgroundJobClient jobs,
        JointDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(principal, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!await db.ConnectionsOf(user.HouseholdId).AnyAsync(c => c.Id == connectionId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        jobs.Enqueue<ConnectionSyncer>(syncer => syncer.SyncAsync(connectionId, CancellationToken.None));
        return TypedResults.Accepted((string?)null);
    }
}
