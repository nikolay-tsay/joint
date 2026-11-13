using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Connectors.LunchFlow;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Joint.Api.Features.Connections;

/// <summary>
/// Links the caller's Lunch Flow account by its API key and starts the first sync.
/// Calling it again replaces the key, which is how a rejected key gets fixed.
/// </summary>
public class ConnectLunchFlow : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/connections/lunch-flow", HandleAsync)
            .WithName("ConnectLunchFlow")
            .RequireAuthorization();

    public static async Task<Results<Ok<ConnectionResponse>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        ConnectLunchFlowRequest request,
        ClaimsPrincipal principal,
        CurrentUserService currentUser,
        LunchFlowConnector lunchFlow,
        ConnectionLinker linker,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(principal, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var apiKey = request.ApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            return TypedResults.Problem("An API key is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await linker.LinkAsync(
            user, lunchFlow, apiKey, "Lunch Flow rejected this API key.", cancellationToken);
    }
}
