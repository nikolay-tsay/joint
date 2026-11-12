using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Me;

/// <summary>Returns the signed-in user and the households they belong to.</summary>
public class GetMe : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/me", HandleAsync).RequireAuthorization();

    private static async Task<IResult> HandleAsync(
        ClaimsPrincipal user, JointDbContext db, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue("sub"), out var userId))
        {
            return Results.Unauthorized();
        }

        var households = await db.Households
            .Where(h => h.Members.Any(m => m.UserId == userId))
            .Select(h => new HouseholdSummary(h.Id, h.Name))
            .ToListAsync(cancellationToken);

        return Results.Ok(new MeResponse(userId, households));
    }
}
