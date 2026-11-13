using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Budget;
using Joint.Api.Features.Categories;
using Joint.Api.Features.Insights;
using Joint.Api.Features.Recurring;
using Joint.Api.Features.Transactions;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class OwnTransferTests
{
    // The endpoints work on the real current month, so everything is booked today.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Money_sent_to_a_payee_marked_as_an_own_transfer_is_not_spending_anywhere()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var topUp = Add(db, account, -5000, "Trading 212 UK Ltd");
        Add(db, account, -2000, "Trading 212 UK Ltd");
        Add(db, account, -300, "Albert");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);
        Assert.Equal(7300m, (await BudgetAsync(db, ana)).Spent);
        Assert.Equal(7300m, (await InsightsAsync(db, ana)).Months[^1].Spending);
        Assert.Equal(7300m, (await CategoriesAsync(db, ana)).Uncategorized);

        var marked = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(topUp.Id, "Transfer", "trading 212"), ana, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(marked.Result);
        var budget = await BudgetAsync(db, ana);
        Assert.Equal(300m, budget.Spent);

        // Not a fixed cost either: it is out of the month altogether.
        Assert.Equal(0m, budget.FixedPaid);
        Assert.Equal(300m, (await InsightsAsync(db, ana)).Months[^1].Spending);
        Assert.Equal(300m, (await CategoriesAsync(db, ana)).Uncategorized);
    }

    [Fact]
    public async Task Money_coming_back_from_an_own_transfer_payee_is_not_income_and_is_labelled()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var topUp = Add(db, account, -5000, "Trading 212 UK Ltd");
        var withdrawal = Add(db, account, 1200, "Trading 212 UK Ltd");
        var salary = Add(db, account, 40000, "Employer");
        await db.SaveChangesAsync();
        Assert.Equal(41200m, (await InsightsAsync(db, ana)).Months[^1].Income);

        await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(topUp.Id, "Transfer"), ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.Equal(40000m, (await InsightsAsync(db, ana)).Months[^1].Income);
        var listed = await ListAsync(db, ana);
        Assert.Equal("Transfer", listed.Single(t => t.Id == topUp.Id).RecurringKind);
        Assert.Equal("Transfer", listed.Single(t => t.Id == withdrawal.Id).RecurringKind);
        Assert.Null(listed.Single(t => t.Id == salary.Id).RecurringKind);
    }

    [Fact]
    public async Task A_mark_matches_on_a_transactions_name_not_on_the_detail_under_it()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var withdrawal = Add(db, account, -3000, "Nikolay Tsay");

        // The bank writes the account holder's name into the detail of unrelated payments.
        var groceries = Add(db, account, -300, "Albert");
        groceries.Description = "Card payment, Nikolay Tsay";
        var unnamed = Add(db, account, -150, "x");
        unnamed.Counterparty = null;
        unnamed.Description = "Nikolay Tsay standing order";
        await db.SaveChangesAsync();

        await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(withdrawal.Id, "Transfer"), ana, new CurrentUserService(db), db, CancellationToken.None);

        var listed = await ListAsync(db, ana);
        Assert.Equal("Transfer", listed.Single(t => t.Id == withdrawal.Id).RecurringKind);
        Assert.Null(listed.Single(t => t.Id == groceries.Id).RecurringKind);

        // With no counterparty, the description is the name the app shows, so it is searched.
        Assert.Equal("Transfer", listed.Single(t => t.Id == unnamed.Id).RecurringKind);
        Assert.Equal(300m, (await BudgetAsync(db, ana)).Spent);
    }

    [Fact]
    public async Task Other_income_is_never_labelled_with_a_subscription_it_happens_to_match()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var payment = Add(db, account, -259, "Netflix");
        var refund = Add(db, account, 259, "Netflix");
        await db.SaveChangesAsync();

        await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(payment.Id, "Subscription"), ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.Null((await ListAsync(db, ana)).Single(t => t.Id == refund.Id).RecurringKind);
        Assert.Equal(259m, (await InsightsAsync(db, ana)).Months[^1].Income);
    }

    [Fact]
    public void An_own_transfer_does_not_appear_in_the_recurring_overview()
    {
        var broker = new RecurringPayment
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Kind = RecurringKind.Transfer, Name = "Trading 212", MatchKey = "TRADING 212",
        };
        var netflix = new RecurringPayment
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Kind = RecurringKind.Subscription, Name = "Netflix", MatchKey = "NETFLIX",
        };

        var overview = RecurringCalculator.Build(
            [broker, netflix],
            [
                new RecurringRow(new DateOnly(2026, 11, 2), -5000, "EUR", "TRADING 212"),
                new RecurringRow(new DateOnly(2026, 11, 9), -5000, "EUR", "TRADING 212"),
                new RecurringRow(new DateOnly(2026, 11, 3), -289, "CZK", "NETFLIX"),
            ],
            new DateOnly(2026, 11, 13), months: 1);

        // The currency follows the costs, not the transfers.
        Assert.Equal("CZK", overview.Currency);
        Assert.Equal((289m, 0m), (overview.Months[0].Subscriptions, overview.Months[0].Bills));
        Assert.Equal("Netflix", Assert.Single(overview.Payments).Name);
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

    private static async Task<CategoriesResponse> CategoriesAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await GetCategories.HandleAsync(null, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<CategoriesResponse>>(result.Result).Value!;
    }

    private static async Task<List<TransactionResponse>> ListAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var listed = await ListTransactions.HandleAsync(
            null, null, null, null, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<TransactionResponse>>>(listed.Result).Value!;
    }

    private static Transaction Add(JointDbContext db, Account account, decimal amount, string counterparty)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            ExternalId = Guid.NewGuid().ToString(),
            BookedOn = Today,
            Amount = amount,
            Currency = "CZK",
            Counterparty = counterparty,
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
