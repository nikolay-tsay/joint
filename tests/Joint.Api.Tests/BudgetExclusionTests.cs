using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Budget;
using Joint.Api.Features.Transactions;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class BudgetExclusionTests
{
    // The budget endpoint works on the real current month, so the payments are booked today.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task An_excluded_payment_no_longer_counts_as_spent_and_can_be_put_back()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var transfer = Add(db, account, -5000);
        Add(db, account, -300);
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);
        Assert.Equal(5300m, await SpentAsync(db, ana));
        var projectedBefore = (await BudgetAsync(db, ana)).Projected;

        var excluded = await UpdateTransaction.HandleAsync(
            transfer.Id, new UpdateTransactionRequest(true), ana, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(excluded.Result);
        Assert.Equal(300m, await SpentAsync(db, ana));

        // The month-end projection is built on the same figure, so it has to move with it.
        var projectedAfter = (await BudgetAsync(db, ana)).Projected;
        Assert.True(projectedAfter < projectedBefore);
        Assert.Equal(decimal.Round(300m / Today.Day * DateTime.DaysInMonth(Today.Year, Today.Month), 2), projectedAfter);
        Assert.True((await ListAsync(db, ana)).Single(t => t.Id == transfer.Id).ExcludedFromBudget);

        await UpdateTransaction.HandleAsync(
            transfer.Id, new UpdateTransactionRequest(false), ana, users, db, CancellationToken.None);
        Assert.Equal(5300m, await SpentAsync(db, ana));
    }

    [Fact]
    public async Task Income_and_other_households_payments_cannot_be_excluded()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var salary = Add(db, account, 40000);
        var payment = Add(db, account, -300);
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        var income = await UpdateTransaction.HandleAsync(
            salary.Id, new UpdateTransactionRequest(true), ana, users, db, CancellationToken.None);
        var stranger = await UpdateTransaction.HandleAsync(
            payment.Id, new UpdateTransactionRequest(true), Principal("ben@example.com"), users, db, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(income.Result);
        Assert.IsType<NotFound>(stranger.Result);
        Assert.All(db.Transactions, t => Assert.False(t.ExcludedFromBudget));
    }

    private static async Task<decimal> SpentAsync(JointDbContext db, ClaimsPrincipal principal) =>
        (await BudgetAsync(db, principal)).Spent;

    private static async Task<BudgetResponse> BudgetAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await GetBudget.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<BudgetResponse>>(result.Result).Value!;
    }

    private static async Task<List<TransactionResponse>> ListAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var listed = await ListTransactions.HandleAsync(
            null, null, null, null, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<TransactionResponse>>>(listed.Result).Value!;
    }

    private static Transaction Add(JointDbContext db, Account account, decimal amount)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            ExternalId = Guid.NewGuid().ToString(),
            BookedOn = Today,
            Amount = amount,
            Currency = "CZK",
            Counterparty = "Somebody",
        };
        db.Transactions.Add(transaction);
        return transaction;
    }

    private static async Task<Account> AccountAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var user = (await new CurrentUserService(db).GetOrCreateAsync(principal, CancellationToken.None))!;
        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Provider = ProviderKind.LunchFlow,
            EncryptedCredential = "x",
        };
        var account = new Account
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            ExternalId = "1",
            Name = "Everyday",
            Currency = "CZK",
        };
        db.BankConnections.Add(connection);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(string email) =>
        new(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", email)], "test"));
}
