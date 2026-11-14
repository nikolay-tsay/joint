using System.Security.Claims;
using System.Security.Cryptography;
using Joint.Api.Common.Auth;
using Joint.Api.Common.Security;
using Joint.Api.Connectors;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Budget;
using Joint.Api.Features.Insights;
using Joint.Api.Features.Sync;
using Joint.Api.Features.Transactions;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Joint.Api.Tests;

public class PendingTransactionTests
{
    private static readonly CredentialProtector Protector = new(RandomNumberGenerator.GetBytes(32));

    // The budget and insights work on the real current month.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Pending_transactions_are_listed_first_and_counted_nowhere()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var connector = new FakeConnector(ProviderKind.LunchFlow)
        {
            Snapshot = Snapshot(
                booked: [Payment("t1", -300, "Albert"), Payment("t2", -200, "Lidl")],
                pending: [Payment("", -99, "Bolt"), Payment("", -1500, "Hotel")]),
        };
        var connection = await ConnectAsync(db, ana);
        await Syncer(db, connector).SyncAsync(connection.Id, CancellationToken.None);

        var firstPage = await ListAsync(db, ana, skip: 0, limit: 1);
        var secondPage = await ListAsync(db, ana, skip: 1, limit: 1);

        // On top of a full page of booked ones, and only on the first page.
        Assert.Equal([true, true, false], firstPage.Select(t => t.IsPending));
        Assert.Equal(["Bolt", "Hotel"], firstPage.Take(2).Select(t => t.Counterparty!).Order());
        Assert.Equal([false], secondPage.Select(t => t.IsPending));
        Assert.Equal(["Albert", "Lidl"], firstPage.Skip(2).Concat(secondPage).Select(t => t.Counterparty!).Order());
        Assert.Empty(db.Transactions.Where(t => t.Amount == -99 || t.Amount == -1500));

        Assert.Equal(500m, (await BudgetAsync(db, ana)).Spent);
        Assert.Equal(500m, (await InsightsAsync(db, ana)).Months[^1].Spending);
    }

    [Fact]
    public async Task Each_sync_replaces_what_is_pending()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var connector = new FakeConnector(ProviderKind.LunchFlow)
        {
            Snapshot = Snapshot(booked: [], pending: [Payment("", -99, "Bolt"), Payment("", -1500, "Hotel")]),
        };
        var connection = await ConnectAsync(db, ana);
        var syncer = Syncer(db, connector);
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        // The taxi was booked; the hotel is still on hold.
        connector.Snapshot = Snapshot(booked: [Payment("t9", -99, "Bolt")], pending: [Payment("", -1500, "Hotel")]);
        await syncer.SyncAsync(connection.Id, CancellationToken.None);

        var listed = await ListAsync(db, ana);
        Assert.Equal([("Hotel", true), ("Bolt", false)], listed.Select(t => (t.Counterparty!, t.IsPending)));
        Assert.Single(db.PendingTransactions);
    }

    [Fact]
    public async Task Pending_transactions_follow_the_filters_and_stay_within_the_household()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var connector = new FakeConnector(ProviderKind.LunchFlow)
        {
            Snapshot = Snapshot(booked: [], pending: [Payment("", -99, "Bolt")]),
        };
        var connection = await ConnectAsync(db, ana);
        await Syncer(db, connector).SyncAsync(connection.Id, CancellationToken.None);

        Assert.Empty(await ListAsync(db, ana, kind: "Savings"));
        Assert.Empty(await ListAsync(db, ana, categories: ["Groceries"]));
        Assert.Single(await ListAsync(db, ana, uncategorized: true));
        Assert.Empty(await ListAsync(db, Principal("ben@example.com")));
    }

    private static ProviderTransaction Payment(string id, decimal amount, string counterparty) =>
        new(id, Today, amount, "CZK", counterparty, null);

    private static ProviderSnapshot Snapshot(List<ProviderTransaction> booked, List<ProviderTransaction> pending) =>
        new([new ProviderAccount("7", "Current account", "Air Bank", AccountKind.Current, "CZK", 0, booked, [], pending)]);

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

    private static async Task<List<TransactionResponse>> ListAsync(
        JointDbContext db, ClaimsPrincipal principal, int? skip = null, int? limit = null, string? kind = null,
        string[]? categories = null, bool uncategorized = false)
    {
        var result = await ListTransactions.HandleAsync(
            null, kind, skip, limit, principal, new CurrentUserService(db), db, CancellationToken.None,
            categories, uncategorized);
        return Assert.IsType<Ok<List<TransactionResponse>>>(result.Result).Value!;
    }

    private static async Task<BudgetResponse> BudgetAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await GetBudget.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<BudgetResponse>>(result.Result).Value!;
    }

    private static async Task<InsightsResponse> InsightsAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await GetInsights.HandleAsync(null, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<InsightsResponse>>(result.Result).Value!;
    }

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(string email) =>
        new(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", email)], "test"));
}
