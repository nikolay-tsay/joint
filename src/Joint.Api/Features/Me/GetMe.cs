using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Me;

/// <summary>
/// Returns the signed-in user and their household. Clients call this right after sign-in;
/// for a new person it is also what creates their user record.
/// </summary>
public class GetMe : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/me", HandleAsync)
            .WithName("GetMe")
            .Produces<MeResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal principal,
        CurrentUserService currentUser,
        JointDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(principal, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var household = await db.Households
            .Where(h => h.Id == user.HouseholdId)
            .Select(h => new HouseholdResponse(
                h.Id,
                h.Name,
                h.Members
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new HouseholdMemberResponse(m.Id, m.DisplayName, m.AvatarUrl))
                    .ToList()))
            .SingleAsync(cancellationToken);

        return Results.Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.AvatarUrl, household));
    }
}
