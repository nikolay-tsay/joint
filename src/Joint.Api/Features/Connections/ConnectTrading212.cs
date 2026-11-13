using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Connectors.Trading212;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Joint.Api.Features.Connections;

/// <summary>
/// Links the caller's Trading 212 account by an API key and secret and starts the first
/// sync. Calling it again replaces them.
/// </summary>
public class ConnectTrading212 : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/connections/trading212", HandleAsync)
            .WithName("ConnectTrading212")
            .RequireAuthorization();

    public static async Task<Results<Ok<ConnectionResponse>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        ConnectTrading212Request request,
        ClaimsPrincipal principal,
        CurrentUserService currentUser,
        Trading212Connector trading212,
        ConnectionLinker linker,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(principal, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey) || string.IsNullOrWhiteSpace(request.ApiSecret))
        {
            return TypedResults.Problem(
                "Both the API key and the API secret are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await linker.LinkAsync(
            user,
            trading212,
            Trading212Connector.PackCredential(request.ApiKey, request.ApiSecret),
            "Trading 212 rejected this key. Check the key and secret, and that the key has the "
            + "Account data and Portfolio permissions.",
            cancellationToken);
    }
}
