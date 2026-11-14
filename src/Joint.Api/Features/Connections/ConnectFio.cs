using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Connectors.Fio;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Joint.Api.Features.Connections;

/// <summary>
/// Links one of the caller's Fio accounts by its read-only API token and starts the first
/// sync. A token is for one account, so a token for another account adds a connection,
/// while a new token for an account already connected replaces the old one, which is how
/// an expired token gets fixed.
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
