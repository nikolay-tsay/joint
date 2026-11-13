using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Categories;

/// <summary>
/// Takes the category off a payee: the payments it matched go back to uncategorized. No
/// transaction is touched. Any household member may do it, whoever made the rule.
/// </summary>
public class RemoveCategoryRule : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/categories/{ruleId:guid}", HandleAsync)
            .WithName("RemoveCategoryRule")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound>> HandleAsync(
        Guid ruleId,
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

        var rule = await db.CategoryRulesOf(user.HouseholdId).FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);
        if (rule is null)
        {
            return TypedResults.NotFound();
        }

        // Another member may have made a rule for the same text; it goes for the whole household.
        var sameText = await db.CategoryRulesOf(user.HouseholdId)
            .Where(r => r.MatchKey == rule.MatchKey)
            .ToListAsync(cancellationToken);
        db.CategoryRules.RemoveRange(sameText);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
