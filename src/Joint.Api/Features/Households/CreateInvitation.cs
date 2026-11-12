using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Joint.Api.Features.Households;

/// <summary>Creates a single-use code that lets another person join the caller's household.</summary>
public class CreateInvitation : IEndpoint
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/household/invitations", HandleAsync)
            .WithName("CreateHouseholdInvitation")
            .RequireAuthorization();

    public static async Task<Results<Ok<CreateInvitationResponse>, UnauthorizedHttpResult>> HandleAsync(
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

        var code = InvitationCode.Generate();
        var invitation = new HouseholdInvitation
        {
            Id = Guid.NewGuid(),
            HouseholdId = user.HouseholdId,
            InvitedByUserId = user.Id,
            CodeHash = InvitationCode.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime),
        };
        db.HouseholdInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new CreateInvitationResponse(code, invitation.ExpiresAt));
    }
}
