using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Joint.Api.Features.Goals;

/// <summary>Adds a saving or investment goal the whole household sees.</summary>
public class CreateGoal : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/goals", HandleAsync)
            .WithName("CreateGoal")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        SaveGoalRequest request,
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

        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        if (GoalRules.Apply(goal, request) is { } problem)
        {
            return TypedResults.Problem(problem, statusCode: StatusCodes.Status400BadRequest);
        }

        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
