using System.Security.Cryptography;
using Joint.Api.Common.Security;
using Joint.Api.Connectors;
using Joint.Api.Data;
using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Sync;

/// <summary>
/// Pulls one connection's data from its provider and stores it: accounts are matched by
/// the provider's id, new transactions are added, holdings are replaced. A failure leaves
/// the stored data untouched and is recorded on the connection for the app to show.
/// </summary>
public class ConnectionSyncer(
    JointDbContext db,
    ConnectorRegistry connectors,
    CredentialProtector protector,
    ILogger<ConnectionSyncer> logger)
{
    /// <summary>
    /// Each sync re-reads this many days before the last one, to pick up transactions
    /// the bank booked late. Duplicates are skipped by id.
    /// </summary>
    public const int OverlapDays = 14;

    public async Task SyncAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await db.BankConnections
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        try
        {
            if (!connectors.TryGet(connection.Provider, out var connector))
            {
                throw new InvalidOperationException($"No connector is available for {connection.Provider}.");
            }

            var credential = protector.Unprotect(connection.EncryptedCredential);
            DateOnly? since = connection.LastSyncedAt is { } last
                ? DateOnly.FromDateTime(last.UtcDateTime).AddDays(-OverlapDays)
                : null;

            var snapshot = await connector.FetchAsync(credential, since, cancellationToken);
            foreach (var fetched in snapshot.Accounts)
            {
                await StoreAccountAsync(connection, fetched, cancellationToken);
            }

            connection.Status = ConnectionStatus.Active;
            connection.LastSyncedAt = DateTimeOffset.UtcNow;
            connection.LastError = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Sync failed for connection {ConnectionId}.", connectionId);
            await RecordFailureAsync(connectionId, DescribeFailure(exception), cancellationToken);
        }
    }

    private async Task StoreAccountAsync(
        BankConnection connection, ProviderAccount fetched, CancellationToken cancellationToken)
    {
        var account = connection.Accounts.FirstOrDefault(a => a.ExternalId == fetched.ExternalId);
        if (account is null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                ConnectionId = connection.Id,
                ExternalId = fetched.ExternalId,
                Name = fetched.Name,
                Currency = fetched.Currency,
                // The provider's guess is only a starting point; after this the user decides.
                Kind = fetched.Kind,
            };
            connection.Accounts.Add(account);
            db.Accounts.Add(account);
        }

        account.Name = fetched.Name;
        account.Institution = fetched.Institution;
        account.Currency = fetched.Currency;
        account.Balance = fetched.Balance;

        var fetchedIds = fetched.Transactions.Select(t => t.ExternalId).ToList();
        var known = (await db.Transactions
                .Where(t => t.AccountId == account.Id && fetchedIds.Contains(t.ExternalId))
                .Select(t => t.ExternalId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var transaction in fetched.Transactions)
        {
            if (!known.Add(transaction.ExternalId))
            {
                continue;
            }

            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                ExternalId = transaction.ExternalId,
                BookedOn = transaction.BookedOn,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                Counterparty = transaction.Counterparty,
                Description = transaction.Description,
            });
        }

        // Holdings are current state, not history: replace them wholesale.
        var oldHoldings = await db.Holdings.Where(h => h.AccountId == account.Id).ToListAsync(cancellationToken);
        db.Holdings.RemoveRange(oldHoldings);
        foreach (var holding in fetched.Holdings)
        {
            db.Holdings.Add(new Holding
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Ticker = holding.Ticker,
                Name = holding.Name,
                Quantity = holding.Quantity,
                AveragePrice = holding.AveragePrice,
                CurrentPrice = holding.CurrentPrice,
                Value = holding.Value,
                UnrealizedProfitLoss = holding.UnrealizedProfitLoss,
                Currency = holding.Currency,
            });
        }
    }

    private async Task RecordFailureAsync(Guid connectionId, string error, CancellationToken cancellationToken)
    {
        // Drop whatever the failed attempt staged, so no half-synced data is saved.
        db.ChangeTracker.Clear();
        var connection = await db.BankConnections.FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        connection.Status = ConnectionStatus.Failing;
        connection.LastError = error;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A message safe to show the user; details stay in the server log.</summary>
    private static string DescribeFailure(Exception exception) => exception switch
    {
        ProviderAuthenticationException => "The provider no longer accepts the saved key. Reconnect to continue.",
        CryptographicException => "The saved key could not be read. Reconnect to continue.",
        HttpRequestException => "The provider could not be reached. It will be retried.",
        InvalidOperationException invalid => invalid.Message,
        _ => "Sync failed unexpectedly. It will be retried.",
    };
}
