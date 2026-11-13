using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Goals;

/// <summary>
/// Changes a goal: its kind, its name, target, what it follows, or the amount kept by hand.
/// Any household member may do it, whoever made the goal.
/// </summary>
public class UpdateGoal : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/goals/{goalId:guid}", HandleAsync)
            .WithName("UpdateGoal")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound, ProblemHttpResult>> HandleAsync(
        Guid goalId,
        SaveGoalRequest request,
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

        if (await GoalRules.ApplyAsync(goal, request, user.HouseholdId, db, cancellationToken) is { } problem)
        {
            return TypedResults.Problem(problem, statusCode: StatusCodes.Status400BadRequest);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
