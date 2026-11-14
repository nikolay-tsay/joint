using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Budget;

/// <summary>
/// Takes a category out of the budget: what was set aside for it goes back to "everything
/// else". The category itself stays.
/// </summary>
public class RemoveCategoryBudget : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/budget/categories", HandleAsync)
            .WithName("RemoveCategoryBudget")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound>> HandleAsync(
        string name,
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

        var line = (await db.CategoryBudgetsOf(user.HouseholdId).ToListAsync(cancellationToken))
            .FirstOrDefault(l => string.Equals(l.Category, name?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            return TypedResults.NotFound();
        }

        db.CategoryBudgets.Remove(line);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
