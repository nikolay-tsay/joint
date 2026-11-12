using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Connections;

/// <summary>
/// Removes one of the caller's own connections together with its stored key, accounts
/// and transactions. Other members' connections cannot be removed.
/// </summary>
public class DeleteConnection : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/connections/{connectionId:guid}", HandleAsync)
            .WithName("DeleteConnection")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound>> HandleAsync(
        Guid connectionId,
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

        var connection = await db.BankConnections.FirstOrDefaultAsync(
            c => c.Id == connectionId && c.OwnerUserId == user.Id, cancellationToken);
        if (connection is null)
        {
            return TypedResults.NotFound();
        }

        // Accounts, transactions and holdings go with it through cascading deletes.
        db.BankConnections.Remove(connection);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
