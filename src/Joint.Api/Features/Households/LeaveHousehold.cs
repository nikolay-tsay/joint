using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Households;

/// <summary>
/// Takes the caller out of a shared household into a new one of their own. Their bank
/// connections go with them, so the people left behind stop seeing them.
/// </summary>
public class LeaveHousehold : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/household/leave", HandleAsync)
            .WithName("LeaveHousehold")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
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

        var sharesHousehold = await db.Users
            .AnyAsync(u => u.HouseholdId == user.HouseholdId && u.Id != user.Id, cancellationToken);
        if (!sharesHousehold)
        {
            return TypedResults.Problem(
                "You are the only member of your household.", statusCode: StatusCodes.Status409Conflict);
        }

        var previousHouseholdId = user.HouseholdId;
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = $"{user.DisplayName}'s household",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Households.Add(household);
        user.HouseholdId = household.Id;

        await HouseholdCleanup.AfterMemberLeftAsync(db, previousHouseholdId, user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
