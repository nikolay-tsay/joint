using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Households;

/// <summary>
/// Moves the caller into the household that issued the code. From then on both people
/// see the same data. The caller's bank connections come with them.
/// </summary>
public class AcceptInvitation : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/household/invitations/accept", HandleAsync)
            .WithName("AcceptHouseholdInvitation")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        AcceptInvitationRequest request,
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

        var codeHash = InvitationCode.Hash(request.Code ?? "");
        var invitation = await db.HouseholdInvitations
            .FirstOrDefaultAsync(i => i.CodeHash == codeHash, cancellationToken);

        // One answer for unknown, used and expired codes, so a guess reveals nothing.
        if (invitation is null || invitation.AcceptedAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return TypedResults.Problem(
                "This invitation code is invalid or has expired.", statusCode: StatusCodes.Status404NotFound);
        }

        if (invitation.HouseholdId == user.HouseholdId)
        {
            return TypedResults.Problem(
                "You are already a member of this household.", statusCode: StatusCodes.Status409Conflict);
        }

        var previousHouseholdId = user.HouseholdId;
        user.HouseholdId = invitation.HouseholdId;
        invitation.AcceptedByUserId = user.Id;
        invitation.AcceptedAt = DateTimeOffset.UtcNow;

        await HouseholdCleanup.AfterMemberLeftAsync(db, previousHouseholdId, user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
