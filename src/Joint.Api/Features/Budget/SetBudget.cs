using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Budget;

/// <summary>
/// Sets, changes or removes the household's monthly budget. Any member may do it, since
/// there is one budget and they all share it.
/// </summary>
public class SetBudget : IEndpoint
{
    /// <summary>Keeps a slip of the finger from storing something absurd.</summary>
    public const decimal Largest = 1_000_000_000m;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/budget", HandleAsync)
            .WithName("SetBudget")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        SetBudgetRequest request,
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

        if (request.Limit is <= 0 or > Largest)
        {
            return TypedResults.Problem(
                "A budget is an amount above zero.", statusCode: StatusCodes.Status400BadRequest);
        }

        var household = await db.Households.FirstAsync(h => h.Id == user.HouseholdId, cancellationToken);
        household.MonthlyBudget = request.Limit;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
