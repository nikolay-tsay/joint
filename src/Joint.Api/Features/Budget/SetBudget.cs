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
/// Sets, changes or removes the household's monthly budget. Any member may do it, since
/// there is one budget and they all share it. Removing it removes its category lines; lowering
/// it below what they add up to shrinks each in proportion. Sent with categories, it sets the
/// budget and its parts in one go.
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
        var lines = await db.CategoryBudgetsOf(user.HouseholdId).ToListAsync(cancellationToken);
        if (request.Limit is not { } limit)
        {
            db.CategoryBudgets.RemoveRange(lines);
        }
        else if (request.Categories is null)
        {
            BudgetLines.ShrinkTo(limit, lines);
        }
        else
        {
            // The budget and its parts arrive together (the app sends them when a budget is
            // made), so they are checked together and either all saved or none.
            var used = await db.CategoryRulesOf(user.HouseholdId).Select(r => r.Category).ToListAsync(cancellationToken);
            var available = CategoryNames.Available(used, household.HiddenCategories);
            var parts = new List<CategoryBudget>();
            foreach (var part in request.Categories)
            {
                var name = BudgetLines.CategoryNamed(available, part.Category);
                if (name is null)
                {
                    return Problem($"\"{part.Category}\" is not one of your categories.");
                }

                if (part.Amount is <= 0 or > Largest)
                {
                    return Problem("A category's part of the budget is an amount above zero.");
                }

                if (parts.Any(p => p.Category == name))
                {
                    return Problem($"{name} is given a part twice.");
                }

                parts.Add(new CategoryBudget
                {
                    Id = Guid.NewGuid(), HouseholdId = user.HouseholdId, Category = name, Amount = part.Amount,
                });
            }

            if (parts.Sum(p => p.Amount) > limit)
            {
                return Problem("The categories add up to more than the budget.");
            }

            db.CategoryBudgets.RemoveRange(lines);
            db.CategoryBudgets.AddRange(parts);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult Problem(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}
