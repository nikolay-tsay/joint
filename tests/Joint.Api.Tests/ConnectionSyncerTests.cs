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
using Joint.Api.Features.Transactions;
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
        Assert.Empty(await TransactionsAsync(db, Ben));
        // Naming someone else's account must not open it either.
        Assert.Empty(await TransactionsAsync(db, Ben, accountId));

        await JoinAsync(db, inviter: Ana, joiner: Ben);

        var shared = Assert.Single(await AccountsAsync(db, Ben));
        Assert.Equal("ana", shared.OwnerDisplayName);
        var transaction = Assert.Single(await TransactionsAsync(db, Ben, accountId));
        Assert.Equal("Current account", transaction.AccountName);
        Assert.Equal("ana", transaction.OwnerDisplayName);
    }

    [Fact]
    public async Task An_account_type_set_by_the_household_survives_later_syncs()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow) { Snapshot = Snapshot(1000, "t1") };
        var connection = await ConnectAsync(db, Ana);
        var syncer = Syncer(db, connector);
        await syncer.SyncAsync(connection.Id, CancellationToken.None);
        var accountId = (await db.Accounts.SingleAsync()).Id;
        await JoinAsync(db, inviter: Ana, joiner: Ben);

        // Ben is in the household, so he may recategorize Ana's account.
        var result = await SetKindAsync(db, Ben, accountId, "savings");
        Assert.IsType<NoContent>(result.Result);

        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        Assert.Equal("Savings", Assert.Single(await AccountsAsync(db, Ana)).Kind);
    }

    [Fact]
    public async Task Holdings_are_replaced_on_each_sync_and_listed_largest_first()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.Trading212)
        {
            Snapshot = Portfolio(
                new ProviderHolding("AAPL", "Apple", 2, 150, 180, "USD", 330, 55),
                new ProviderHolding("IWDA", "World ETF", 10, 80, 76, "EUR", 760, -40)),
        };
        var connection = await ConnectAsync(db, Ana, ProviderKind.Trading212);
        var syncer = Syncer(db, connector);
        await syncer.SyncAsync(connection.Id, CancellationToken.None);
        var account = await db.Accounts.SingleAsync();
        Assert.Equal(AccountKind.Investment, account.Kind);

        var first = await HoldingsAsync(db, Ana, account.Id);
        Assert.Equal(["World ETF", "Apple"], first.Select(h => h.Name));
        Assert.Equal("EUR", first[0].ValueCurrency);
        Assert.Equal("USD", first[1].PriceCurrency);
        Assert.Equal(55, first[1].UnrealizedProfitLoss);

        // Apple was sold; a new position appeared.
        connector.Snapshot = Portfolio(
            new ProviderHolding("IWDA", "World ETF", 12, 79, 77, "EUR", 924, -24),
            new ProviderHolding("MSFT", "Microsoft", 1, 400, 410, "USD", 380, 9));
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        var second = await HoldingsAsync(db, Ana, account.Id);
        Assert.Equal(["World ETF", "Microsoft"], second.Select(h => h.Name));
        Assert.Equal(12, second[0].Quantity);
        Assert.Equal(2, await db.Holdings.CountAsync());
    }

    [Fact]
    public async Task Someone_outside_the_household_cannot_see_holdings()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.Trading212)
        {
            Snapshot = Portfolio(new ProviderHolding("AAPL", "Apple", 2, 150, 180, "USD", 330, 55)),
        };
        var connection = await ConnectAsync(db, Ana, ProviderKind.Trading212);
        await Syncer(db, connector).SyncAsync(connection.Id, CancellationToken.None);
        var accountId = (await db.Accounts.SingleAsync()).Id;

        var result = await ListAccountHoldings.HandleAsync(
            accountId, Ben, new CurrentUserService(db), db, CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task Transactions_can_be_narrowed_to_one_kind_of_account()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow)
        {
            Snapshot = new ProviderSnapshot([
                new ProviderAccount(
                    "everyday", "Current account", "Air Bank", AccountKind.Current, "CZK", 0,
                    [new ProviderTransaction("groceries", new DateOnly(2026, 11, 2), -100, "CZK", null, null)], []),
                new ProviderAccount(
                    "pot", "Rainy day", "Air Bank", AccountKind.Current, "CZK", 0,
                    [new ProviderTransaction("deposit", new DateOnly(2026, 11, 1), 500, "CZK", null, null)], []),
            ]),
        };
        var connection = await ConnectAsync(db, Ana);
        await Syncer(db, connector).SyncAsync(connection.Id, CancellationToken.None);
        var pot = await db.Accounts.SingleAsync(a => a.ExternalId == "pot");
        await SetKindAsync(db, Ana, pot.Id, "Savings");

        var everyday = await TransactionsAsync(db, Ana, kind: "Current");
        var savings = await TransactionsAsync(db, Ana, kind: "savings");
        var all = await TransactionsAsync(db, Ana);

        Assert.Equal("Current account", Assert.Single(everyday).AccountName);
        Assert.Equal("Rainy day", Assert.Single(savings).AccountName);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task An_unknown_kind_filter_is_rejected()
    {
        await using var db = NewDb();

        var result = await ListTransactions.HandleAsync(
            null, "Pension", null, null, Ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.Equal(400, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData("Pension")]
    [InlineData("1")]
    [InlineData("")]
    public async Task An_unknown_account_type_is_rejected(string kind)
    {
        await using var db = NewDb();
        var connection = await ConnectAsync(db, Ana);
        await Syncer(db, new FakeConnector(ProviderKind.LunchFlow) { Snapshot = Snapshot(1, "t1") })
            .SyncAsync(connection.Id, CancellationToken.None);
        var accountId = (await db.Accounts.SingleAsync()).Id;

        var result = await SetKindAsync(db, Ana, accountId, kind);

        Assert.Equal(400, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Someone_outside_the_household_cannot_change_an_account()
    {
        await using var db = NewDb();
        var connection = await ConnectAsync(db, Ana);
        await Syncer(db, new FakeConnector(ProviderKind.LunchFlow) { Snapshot = Snapshot(1, "t1") })
            .SyncAsync(connection.Id, CancellationToken.None);
        var accountId = (await db.Accounts.SingleAsync()).Id;

        var result = await SetKindAsync(db, Ben, accountId, "Savings");

        Assert.IsType<NotFound>(result.Result);
        Assert.Equal(AccountKind.Current, (await db.Accounts.SingleAsync()).Kind);
    }

    [Fact]
    public async Task Transactions_come_newest_first_and_page_with_skip_and_limit()
    {
        await using var db = NewDb();
        var connector = new FakeConnector(ProviderKind.LunchFlow)
        {
            Snapshot = new ProviderSnapshot([
                new ProviderAccount(
                    "7", "Current account", "Air Bank", AccountKind.Current, "CZK", 0,
                    Enumerable.Range(1, 5)
                        .Select(day => new ProviderTransaction(
                            $"t{day}", new DateOnly(2026, 11, day), -day, "CZK", null, null))
                        .ToList(),
                    []),
            ]),
        };
        var connection = await ConnectAsync(db, Ana);
        await Syncer(db, connector).SyncAsync(connection.Id, CancellationToken.None);

        var firstPage = await TransactionsAsync(db, Ana, skip: 0, limit: 2);
        var secondPage = await TransactionsAsync(db, Ana, skip: 2, limit: 2);

        Assert.Equal([5, 4], firstPage.Select(t => t.BookedOn.Day));
        Assert.Equal([3, 2], secondPage.Select(t => t.BookedOn.Day));
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

    private static ProviderSnapshot Portfolio(params ProviderHolding[] holdings) =>
        new([new ProviderAccount("42", "Trading 212", "Trading 212", AccountKind.Investment, "EUR", 1000, [], holdings)]);

    private static async Task<List<HoldingResponse>> HoldingsAsync(
        JointDbContext db, ClaimsPrincipal principal, Guid accountId)
    {
        var result = await ListAccountHoldings.HandleAsync(
            accountId, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<HoldingResponse>>>(result.Result).Value!;
    }

    private static async Task<BankConnection> ConnectAsync(
        JointDbContext db, ClaimsPrincipal principal, ProviderKind provider = ProviderKind.LunchFlow)
    {
        var user = (await new CurrentUserService(db).GetOrCreateAsync(principal, CancellationToken.None))!;
        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Provider = provider,
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

    private static Task<Results<NoContent, UnauthorizedHttpResult, NotFound, ProblemHttpResult>> SetKindAsync(
        JointDbContext db, ClaimsPrincipal principal, Guid accountId, string kind) =>
        UpdateAccount.HandleAsync(
            accountId, new UpdateAccountRequest(kind), principal, new CurrentUserService(db), db, CancellationToken.None);

    private static async Task<List<TransactionResponse>> TransactionsAsync(
        JointDbContext db, ClaimsPrincipal principal, Guid? accountId = null, int? skip = null, int? limit = null,
        string? kind = null)
    {
        var result = await ListTransactions.HandleAsync(
            accountId, kind, skip, limit, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<TransactionResponse>>>(result.Result).Value!;
    }

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
