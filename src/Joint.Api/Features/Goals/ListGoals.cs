using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Goals;

/// <summary>
/// The household's saving and investment goals, oldest first. Every saving goal is
/// measured against the combined balance of the accounts of the Savings type, every
/// investment goal against those of the Investment type.
/// </summary>
public class ListGoals : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/goals", HandleAsync)
            .WithName("ListGoals")
            .RequireAuthorization();

    public static async Task<Results<Ok<List<GoalResponse>>, UnauthorizedHttpResult>> HandleAsync(
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

        var goals = await db.GoalsOf(user.HouseholdId).OrderBy(g => g.CreatedAt).ToListAsync(cancellationToken);
        var accounts = await db.AccountsOf(user.HouseholdId).ToListAsync(cancellationToken);

        return TypedResults.Ok(goals
            .Select(goal =>
            {
                var (reached, currency) = GoalRules.Progress(goal.Kind, accounts);
                return new GoalResponse(goal.Id, goal.Name, goal.TargetAmount, reached, currency, goal.Kind.ToString());
            })
            .ToList());
    }
}
