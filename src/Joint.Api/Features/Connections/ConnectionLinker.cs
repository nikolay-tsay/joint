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
/// per provider, so connecting again replaces the credential; the exception is a provider
/// whose credential is for a single account, where there is one connection per account.
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
        CredentialCheck check;
        try
        {
            check = await connector.CheckCredentialAsync(credential, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return TypedResults.Problem(
                "The provider could not be reached. Try again shortly.", statusCode: StatusCodes.Status502BadGateway);
        }

        if (!check.Accepted)
        {
            return TypedResults.Problem(rejectedMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        var mine = await db.BankConnections
            .Include(c => c.Accounts)
            .Where(c => c.OwnerUserId == user.Id && c.Provider == connector.Provider)
            .ToListAsync(cancellationToken);
        var connection = Existing(mine, check.AccountKey);
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

        connection.AccountKey = check.AccountKey;
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
            connection.LastError,
            connection.Accounts.Select(a => a.Name).Order().ToList()));
    }

    /// <summary>
    /// The connection a credential belongs to, if the person already has it. A credential
    /// that covers everything at the provider has one connection, and connecting again
    /// replaces it. A credential for one account (<paramref name="accountKey"/>) replaces
    /// only the connection for that same account; for any other account it is a new one.
    /// </summary>
    private static BankConnection? Existing(List<BankConnection> mine, string? accountKey)
    {
        if (accountKey is null)
        {
            return mine.FirstOrDefault();
        }

        return mine.FirstOrDefault(c => c.AccountKey == accountKey)
            // Connected before the key was recorded: recognised by the account it synced,
            // or, never having synced at all, taken to be the one being retried.
            ?? mine.FirstOrDefault(c => c.AccountKey is null && c.Accounts.Any(a => a.ExternalId == accountKey))
            ?? mine.FirstOrDefault(c => c.AccountKey is null && c.Accounts.Count == 0);
    }
}
