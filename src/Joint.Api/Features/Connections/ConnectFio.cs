using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Connectors.Fio;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Joint.Api.Features.Connections;

/// <summary>
/// Links the caller's Fio account by its read-only API token and starts the first sync.
/// Calling it again replaces the token, which is how an expired one gets fixed.
/// </summary>
public class ConnectFio : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/connections/fio", HandleAsync)
            .WithName("ConnectFio")
            .RequireAuthorization();

    public static async Task<Results<Ok<ConnectionResponse>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        ConnectFioRequest request,
        ClaimsPrincipal principal,
        CurrentUserService currentUser,
        FioConnector fio,
        ConnectionLinker linker,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(principal, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var token = request.Token?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            return TypedResults.Problem("A token is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await linker.LinkAsync(user, fio, token, "Fio rejected this token.", cancellationToken);
    }
}
