using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Goals;

/// <summary>
/// The household's saving and investment goals, oldest first, each with how much is saved: the followed
/// account's current balance, or the figure kept by hand.
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

        // Through the household's accounts, so a goal never reveals an account that has left with its owner.
        var accounts = await db.AccountsOf(user.HouseholdId).ToDictionaryAsync(a => a.Id, cancellationToken);

        return TypedResults.Ok(goals
            .Select(goal =>
            {
                var followed = goal.AccountId is { } id ? accounts.GetValueOrDefault(id) : null;
                return new GoalResponse(
                    goal.Id,
                    goal.Name,
                    goal.TargetAmount,
                    Saved: followed?.Balance ?? goal.SavedAmount,
                    followed?.Currency ?? goal.Currency,
                    followed?.Id,
                    followed?.Name,
                    goal.Kind.ToString());
            })
            .ToList());
    }
}
