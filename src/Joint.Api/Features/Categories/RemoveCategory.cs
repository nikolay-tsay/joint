using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Categories;

/// <summary>
/// Deletes a category altogether: every payee in it, whoever put it there, goes back to
/// uncategorized, and the name leaves the list. A category of the household's own leaves
/// it by itself, since a name exists only while a rule uses it; a starter category is
/// recorded as deleted on the household. No transaction is touched. Any household member
/// may do it. <c>name</c> is matched without regard to capitals, like a name typed when
/// assigning.
/// </summary>
public class RemoveCategory : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/categories", HandleAsync)
            .WithName("RemoveCategory")
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

        var wanted = name?.Trim() ?? "";
        bool Same(string other) => string.Equals(other, wanted, StringComparison.OrdinalIgnoreCase);

        // Compared in memory: a household has a few dozen rules, and the comparison has to
        // ignore capitals the same way CategoryNames does.
        var rules = (await db.CategoryRulesOf(user.HouseholdId).ToListAsync(cancellationToken))
            .Where(r => Same(r.Category))
            .ToList();
        var household = await db.Households.FirstAsync(h => h.Id == user.HouseholdId, cancellationToken);
        var starter = CategoryNames.Starter.FirstOrDefault(Same);
        var hideStarter = starter is not null && !household.HiddenCategories.Any(Same);
        if (rules.Count == 0 && !hideStarter)
        {
            return TypedResults.NotFound();
        }

        db.CategoryRules.RemoveRange(rules);
        if (hideStarter)
        {
            // A new list rather than an edit in place, so the change is noticed and saved.
            household.HiddenCategories = [.. household.HiddenCategories, starter!];
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
