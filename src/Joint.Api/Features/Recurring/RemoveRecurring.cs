using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Recurring;

/// <summary>
/// Stops tracking a payee. No transaction is touched. Any household member may do it,
/// whoever marked it, since they all share one view.
/// </summary>
public class RemoveRecurring : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/recurring/{recurringId:guid}", HandleAsync)
            .WithName("RemoveRecurring")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound>> HandleAsync(
        Guid recurringId,
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

        var marked = await db.RecurringOf(user.HouseholdId)
            .FirstOrDefaultAsync(r => r.Id == recurringId, cancellationToken);
        if (marked is null)
        {
            return TypedResults.NotFound();
        }

        // Another member may have marked the same payee; it goes for the whole household.
        var samePayee = await db.RecurringOf(user.HouseholdId)
            .Where(r => r.MatchKey == marked.MatchKey)
            .ToListAsync(cancellationToken);
        db.RecurringPayments.RemoveRange(samePayee);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
