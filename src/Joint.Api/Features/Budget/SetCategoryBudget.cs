using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Categories;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Budget;

/// <summary>
/// Sets aside part of the monthly budget for one of the household's categories, or changes
/// what is set aside. The lines together must fit within the budget, so a budget has to be
/// set first. Any member may do it, as with the budget.
/// </summary>
public class SetCategoryBudget : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/budget/categories", HandleAsync)
            .WithName("SetCategoryBudget")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        SetCategoryBudgetRequest request,
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

        var household = await db.Households.FirstAsync(h => h.Id == user.HouseholdId, cancellationToken);
        if (household.MonthlyBudget is not { } limit)
        {
            return Problem("Set a monthly budget first.");
        }

        if (request.Amount is <= 0 or > SetBudget.Largest)
        {
            return Problem("A category's part of the budget is an amount above zero.");
        }

        // Only a category the household has: the budget does not create new ones.
        var used = await db.CategoryRulesOf(user.HouseholdId).Select(r => r.Category).ToListAsync(cancellationToken);
        var name = BudgetLines.CategoryNamed(CategoryNames.Available(used, household.HiddenCategories), request.Category);
        if (name is null)
        {
            return Problem("That is not one of your categories.");
        }

        var lines = await db.CategoryBudgetsOf(user.HouseholdId).ToListAsync(cancellationToken);
        var room = BudgetLines.Unassigned(limit, lines, except: name);
        if (request.Amount > room)
        {
            return Problem($"Only {room:0.##} of the budget is left for categories.");
        }

        var line = lines.FirstOrDefault(l => string.Equals(l.Category, name, StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            db.CategoryBudgets.Add(new CategoryBudget
            {
                Id = Guid.NewGuid(),
                HouseholdId = user.HouseholdId,
                Category = name,
                Amount = request.Amount,
            });
        }
        else
        {
            line.Category = name;
            line.Amount = request.Amount;
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult Problem(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}
