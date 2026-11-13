using Hangfire;
using Joint.Api.Common.Security;
using Joint.Api.Connectors;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Sync;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Connections;

/// <summary>
/// What every "connect a provider" endpoint does once it has a credential: check it with
/// the provider, store it encrypted, and start the first sync. A user has one connection
/// per provider, so connecting again replaces the credential.
/// </summary>
public class ConnectionLinker(JointDbContext db, CredentialProtector protector, IBackgroundJobClient jobs)
{
    /// <param name="rejectedMessage">Shown when the provider does not accept the credential.</param>
    public async Task<Results<Ok<ConnectionResponse>, UnauthorizedHttpResult, ProblemHttpResult>> LinkAsync(
        User user,
        IProviderConnector connector,
        string credential,
        string rejectedMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await connector.ValidateCredentialAsync(credential, cancellationToken))
            {
                return TypedResults.Problem(rejectedMessage, statusCode: StatusCodes.Status400BadRequest);
            }
        }
        catch (HttpRequestException)
        {
            return TypedResults.Problem(
                "The provider could not be reached. Try again shortly.", statusCode: StatusCodes.Status502BadGateway);
        }

        var connection = await db.BankConnections.FirstOrDefaultAsync(
            c => c.OwnerUserId == user.Id && c.Provider == connector.Provider, cancellationToken);
        if (connection is null)
        {
            connection = new BankConnection
            {
                Id = Guid.NewGuid(),
                OwnerUserId = user.Id,
                Provider = connector.Provider,
                EncryptedCredential = "",
            };
            db.BankConnections.Add(connection);
        }

        connection.EncryptedCredential = protector.Protect(credential);
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
