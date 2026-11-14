using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Connections;

/// <summary>Lists the household's connections with their sync state.</summary>
public class ListConnections : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/connections", HandleAsync)
            .WithName("ListConnections")
            .RequireAuthorization();

    public static async Task<Results<Ok<List<ConnectionResponse>>, UnauthorizedHttpResult>> HandleAsync(
        ClaimsPrincipal principal,
        CurrentUserService currentUser,
        JointDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(principal, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var rows = await (
            from connection in db.ConnectionsOf(user.HouseholdId)
            join owner in db.Users on connection.OwnerUserId equals owner.Id
            orderby owner.DisplayName
            select new { connection, owner.DisplayName }).ToListAsync(cancellationToken);

        // What tells two connections to the same provider apart is the accounts they brought.
        var accounts = (await db.AccountsOf(user.HouseholdId)
                .Select(a => new { a.ConnectionId, a.Name })
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.ConnectionId, a => a.Name);

        return TypedResults.Ok(rows
            .Select(row => new ConnectionResponse(
                row.connection.Id,
                row.connection.Provider.ToString(),
                row.connection.Status.ToString(),
                row.DisplayName,
                row.connection.OwnerUserId == user.Id,
                row.connection.LastSyncedAt,
                row.connection.LastError,
                accounts[row.connection.Id].Order().ToList()))
            .ToList());
    }
}
