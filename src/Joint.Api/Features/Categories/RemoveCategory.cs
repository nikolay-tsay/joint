using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Categories;

/// <summary>
/// Removes a whole category: every payee in it, whoever put it there, goes back to
/// uncategorized. A category of the household's own then disappears from the list, since a
/// name exists only while a rule uses it; a starter category stays on offer, just empty.
/// No transaction is touched. Any household member may do it. <c>name</c> is matched
/// without regard to capitals, like a name typed when assigning.
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

        // Compared in memory: a household has a few dozen rules, and the comparison has to
        // ignore capitals the same way CategoryNames does.
        var rules = (await db.CategoryRulesOf(user.HouseholdId).ToListAsync(cancellationToken))
            .Where(r => string.Equals(r.Category, name?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (rules.Count == 0)
        {
            return TypedResults.NotFound();
        }

        db.CategoryRules.RemoveRange(rules);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
