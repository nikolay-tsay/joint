using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Goals;

/// <summary>Removes a goal. No account or transaction is touched.</summary>
public class DeleteGoal : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/goals/{goalId:guid}", HandleAsync)
            .WithName("DeleteGoal")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound>> HandleAsync(
        Guid goalId,
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

        var goal = await db.GoalsOf(user.HouseholdId).FirstOrDefaultAsync(g => g.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return TypedResults.NotFound();
        }

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
