using System.Security.Claims;
using System.Security.Cryptography;
using Joint.Api.Common.Auth;
using Joint.Api.Common.Security;
using Joint.Api.Connectors;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Accounts;
using Joint.Api.Features.Households;
using Joint.Api.Features.Sync;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Joint.Api.Tests;

public class ConnectionSyncerTests
{
    private static readonly CredentialProtector Protector = new(RandomNumberGenerator.GetBytes(32));
    private static readonly ClaimsPrincipal Ana = Principal("ana@example.com");
    private static readonly ClaimsPrincipal Ben = Principal("ben@example.com");

    [Fact]
    public async Task First_sync_stores_accounts_and_transactions_and_marks_the_connection_active()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow) { Snapshot = Snapshot(1000, "t1", "t2") };
        var connection = await ConnectAsync(db, Ana);

        await Syncer(db, connector).SyncAsync(connection.Id, CancellationToken.None);

        var account = await db.Accounts.SingleAsync();
        Assert.Equal("Air Bank", account.Institution);
        Assert.Equal(1000, account.Balance);
        Assert.Equal(2, await db.Transactions.CountAsync());

        var synced = await db.BankConnections.SingleAsync();
        Assert.Equal(ConnectionStatus.Active, synced.Status);
        Assert.NotNull(synced.LastSyncedAt);
        Assert.Null(connector.FetchedSince.Single());
    }

    [Fact]
    public async Task Later_syncs_update_the_balance_and_add_only_new_transactions()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow) { Snapshot = Snapshot(1000, "t1", "t2") };
        var connection = await ConnectAsync(db, Ana);
        var syncer = Syncer(db, connector);
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        connector.Snapshot = Snapshot(750, "t2", "t3");
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        Assert.Equal(750, (await db.Accounts.SingleAsync()).Balance);
        Assert.Equal(
            ["t1", "t2", "t3"],
            await db.Transactions.Select(t => t.ExternalId).OrderBy(id => id).ToListAsync());

        // The second fetch reaches back before the first sync to catch late bookings.
        var since = connector.FetchedSince[1];
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-ConnectionSyncer.OverlapDays), since);
    }

    [Fact]
    public async Task A_rejected_key_marks_the_connection_failing_and_keeps_existing_data()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow) { Snapshot = Snapshot(1000, "t1") };
        var connection = await ConnectAsync(db, Ana);
        var syncer = Syncer(db, connector);
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        connector.Failure = new ProviderAuthenticationException("rejected");
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        var failed = await db.BankConnections.SingleAsync();
        Assert.Equal(ConnectionStatus.Failing, failed.Status);
        Assert.Contains("Reconnect", failed.LastError);
        Assert.Equal(1, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task A_recovered_connection_becomes_active_again()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow) { Failure = new HttpRequestException("down") };
        var connection = await ConnectAsync(db, Ana);
        var syncer = Syncer(db, connector);
        await syncer.SyncAsync(connection.Id, CancellationToken.None);
        Assert.Equal(ConnectionStatus.Failing, (await db.BankConnections.SingleAsync()).Status);

        connector.Failure = null;
        connector.Snapshot = Snapshot(10, "t1");
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        var recovered = await db.BankConnections.SingleAsync();
        Assert.Equal(ConnectionStatus.Active, recovered.Status);
        Assert.Null(recovered.LastError);
    }

    [Fact]
    public async Task Household_members_see_each_others_accounts_and_outsiders_do_not()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow) { Snapshot = Snapshot(1000, "t1") };
        var connection = await ConnectAsync(db, Ana);
        await Syncer(db, connector).SyncAsync(connection.Id, CancellationToken.None);
        var accountId = (await db.Accounts.SingleAsync()).Id;

        Assert.Empty(await AccountsAsync(db, Ben));
        Assert.IsType<NotFound>((await TransactionsAsync(db, Ben, accountId)).Result);

        await JoinAsync(db, inviter: Ana, joiner: Ben);

        var shared = Assert.Single(await AccountsAsync(db, Ben));
        Assert.Equal("ana", shared.OwnerDisplayName);
        var transactions = Assert.IsType<Ok<List<TransactionResponse>>>((await TransactionsAsync(db, Ben, accountId)).Result);
        Assert.Single(transactions.Value!);
    }

    private static ProviderSnapshot Snapshot(decimal balance, params string[] transactionIds) =>
        new([
            new ProviderAccount(
                "7", "Current account", "Air Bank", AccountKind.Current, "CZK", balance,
                transactionIds
                    .Select(id => new ProviderTransaction(id, new DateOnly(2026, 11, 1), -100, "CZK", "Shop", null))
                    .ToList(),
                []),
        ]);

    private static ConnectionSyncer Syncer(JointDbContext db, FakeConnector connector) =>
        new(db, new ConnectorRegistry([connector]), Protector, NullLogger<ConnectionSyncer>.Instance);

    private static async Task<BankConnection> ConnectAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var user = (await new CurrentUserService(db).GetOrCreateAsync(principal, CancellationToken.None))!;
        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Provider = ProviderKind.LunchFlow,
            Status = ConnectionStatus.Pending,
            EncryptedCredential = Protector.Protect("key"),
        };
        db.BankConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection;
    }

    private static async Task<List<AccountResponse>> AccountsAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await ListAccounts.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<AccountResponse>>>(result.Result).Value!;
    }

    private static Task<Results<Ok<List<TransactionResponse>>, UnauthorizedHttpResult, NotFound>> TransactionsAsync(
        JointDbContext db, ClaimsPrincipal principal, Guid accountId) =>
        ListAccountTransactions.HandleAsync(
            accountId, null, null, principal, new CurrentUserService(db), db, CancellationToken.None);

    private static async Task JoinAsync(JointDbContext db, ClaimsPrincipal inviter, ClaimsPrincipal joiner)
    {
        var invitation = await CreateInvitation.HandleAsync(inviter, new CurrentUserService(db), db, CancellationToken.None);
        var code = Assert.IsType<Ok<CreateInvitationResponse>>(invitation.Result).Value!.Code;
        await AcceptInvitation.HandleAsync(
            new AcceptInvitationRequest(code), joiner, new CurrentUserService(db), db, CancellationToken.None);
    }

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(string email) =>
        new(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", email)], "test"));
}
