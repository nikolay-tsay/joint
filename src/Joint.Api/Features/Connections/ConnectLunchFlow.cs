using System.Security.Claims;
using Hangfire;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Common.Security;
using Joint.Api.Connectors.LunchFlow;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Sync;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

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
        CredentialProtector protector,
        IBackgroundJobClient jobs,
        JointDbContext db,
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

        try
        {
            if (!await lunchFlow.ValidateCredentialAsync(apiKey, cancellationToken))
            {
                return TypedResults.Problem(
                    "Lunch Flow rejected this API key.", statusCode: StatusCodes.Status400BadRequest);
            }
        }
        catch (HttpRequestException)
        {
            return TypedResults.Problem(
                "Lunch Flow could not be reached. Try again shortly.", statusCode: StatusCodes.Status502BadGateway);
        }

        var connection = await db.BankConnections.FirstOrDefaultAsync(
            c => c.OwnerUserId == user.Id && c.Provider == ProviderKind.LunchFlow, cancellationToken);
        if (connection is null)
        {
            connection = new BankConnection
            {
                Id = Guid.NewGuid(),
                OwnerUserId = user.Id,
                Provider = ProviderKind.LunchFlow,
                EncryptedCredential = "",
            };
            db.BankConnections.Add(connection);
        }

        connection.EncryptedCredential = protector.Protect(apiKey);
        connection.Status = ConnectionStatus.Pending;
        connection.LastError = null;
        await db.SaveChangesAsync(cancellationToken);

        var connectionId = connection.Id;
        jobs.Enqueue<ConnectionSyncer>(syncer => syncer.SyncAsync(connectionId, CancellationToken.None));

        return TypedResults.Ok(new ConnectionResponse(
            connection.Id,
            connection.Provider.ToString(),
            connection.Status.ToString(),
            user.DisplayName,
            IsMine: true,
            connection.LastSyncedAt,
            connection.LastError));
    }
}
