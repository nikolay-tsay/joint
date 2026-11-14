using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Categories;
using Joint.Api.Features.Transactions;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class CategoryTests
{
    private static readonly DateOnly November = new(2026, 11, 1);
    private static readonly Guid Everyday = Guid.NewGuid();
    private static readonly Guid Savings = Guid.NewGuid();

    [Fact]
    public void A_months_spending_is_split_by_category_with_the_rest_uncategorized()
    {
        var breakdown = CategoryCalculator.Build(
            [
                Row(Everyday, "2026-11-02", -450, "ALBERT 0123"),
                Row(Everyday, "2026-11-09", -620, "ALBERT 0456"),
                Row(Everyday, "2026-11-05", -289, "Netflix"),
                Row(Everyday, "2026-11-06", -1200, "Somewhere"),
                Row(Everyday, "2026-11-10", 40000, "Employer"),
                Row(Everyday, "2026-10-28", -999, "ALBERT 0123"),
            ],
            [Rule("Groceries", "ALBERT"), Rule("Entertainment", "NETFLIX")],
            DatePeriod.MonthOf(November));

        Assert.Equal("CZK", breakdown.Currency);
        Assert.Equal(November, breakdown.Month);
        Assert.Equal(
            [("Groceries", 1070m, 2), ("Entertainment", 289m, 1)],
            breakdown.Spending.Select(c => (c.Name, c.Amount, c.Count)));
        Assert.Equal(1200m, breakdown.Uncategorized);
    }

    [Fact]
    public void Money_moved_between_own_accounts_is_not_spending_in_any_category()
    {
        var breakdown = CategoryCalculator.Build(
            [
                Row(Everyday, "2026-11-30", -5000, "Transfer to savings"),
                Row(Savings, "2026-12-01", 5000, "Transfer from everyday"),
                Row(Everyday, "2026-11-03", -300, "Somewhere"),
            ],
            [Rule("Other", "TRANSFER")],
            DatePeriod.MonthOf(November));

        Assert.Empty(breakdown.Spending);
        Assert.Equal(300m, breakdown.Uncategorized);
    }

    [Fact]
    public void The_names_on_offer_are_the_starter_list_then_the_households_own()
    {
        var breakdown = CategoryCalculator.Build([], [Rule("Pets", "VET"), Rule("Groceries", "ALBERT"), Rule("Garden", "HORNBACH")], DatePeriod.MonthOf(November));

        Assert.Equal(CategoryNames.Starter, breakdown.Names.Take(CategoryNames.Starter.Count));
        Assert.Equal(["Garden", "Pets"], breakdown.Names.Skip(CategoryNames.Starter.Count));
        Assert.Null(breakdown.Currency);
    }

    [Fact]
    public async Task Giving_a_payment_a_category_gives_it_to_every_payment_to_that_payee()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var october = Add(db, account, "2026-10-03", -450, "ALBERT 0123");
        var november = Add(db, account, "2026-11-03", -620, "ALBERT 0456");
        var other = Add(db, account, "2026-11-05", -289, "Netflix");
        await db.SaveChangesAsync();

        var assigned = await AssignCategory.HandleAsync(
            new AssignCategoryRequest(november.Id, "groceries"), ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.IsType<NoContent>(assigned.Result);
        var rule = Assert.Single(db.CategoryRules);

        // Typed in lower case, stored as the starter list spells it.
        Assert.Equal(("Groceries", "ALBERT"), (rule.Category, rule.MatchKey));

        var transactions = await ListAsync(db, ana);
        Assert.Equal("Groceries", transactions.Single(t => t.Id == october.Id).Category);
        Assert.Equal("Groceries", transactions.Single(t => t.Id == november.Id).Category);
        Assert.Equal(rule.Id, transactions.Single(t => t.Id == november.Id).CategoryRuleId);
        Assert.Equal("ALBERT", transactions.Single(t => t.Id == november.Id).CategoryMatch);

        // An uncategorized payment comes with the text a rule for it would use.
        var untouched = transactions.Single(t => t.Id == other.Id);
        Assert.Null(untouched.Category);
        Assert.Equal("NETFLIX", untouched.CategoryMatch);
    }

    [Fact]
    public async Task Choosing_again_changes_the_category_and_a_new_name_becomes_a_category()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var vet = Add(db, account, "2026-11-03", -900, "Veterina Praha");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        await AssignCategory.HandleAsync(new AssignCategoryRequest(vet.Id, "Health"), ana, users, db, CancellationToken.None);
        await AssignCategory.HandleAsync(new AssignCategoryRequest(vet.Id, " Pets ", "veterina"), ana, users, db, CancellationToken.None);

        var rule = Assert.Single(db.CategoryRules);
        Assert.Equal(("Pets", "VETERINA"), (rule.Category, rule.MatchKey));
    }

    [Fact]
    public async Task A_category_needs_a_name_and_text_that_is_in_the_payment()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var payment = Add(db, account, "2026-11-03", -289, "Netflix");
        var salary = Add(db, account, "2026-11-10", 40000, "Employer");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        var noName = await AssignCategory.HandleAsync(new AssignCategoryRequest(payment.Id, "  "), ana, users, db, CancellationToken.None);
        var typo = await AssignCategory.HandleAsync(new AssignCategoryRequest(payment.Id, "Fun", "Netflx"), ana, users, db, CancellationToken.None);
        var income = await AssignCategory.HandleAsync(new AssignCategoryRequest(salary.Id, "Other"), ana, users, db, CancellationToken.None);
        var stranger = await AssignCategory.HandleAsync(
            new AssignCategoryRequest(payment.Id, "Fun"), Principal("ben@example.com"), users, db, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(noName.Result);
        Assert.IsType<ProblemHttpResult>(typo.Result);
        Assert.IsType<ProblemHttpResult>(income.Result);
        Assert.IsType<NotFound>(stranger.Result);
        Assert.Empty(db.CategoryRules);
    }

    [Fact]
    public async Task Removing_a_rule_takes_the_category_off_but_only_within_the_household()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var payment = Add(db, account, "2026-11-03", -289, "Netflix");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);
        await AssignCategory.HandleAsync(new AssignCategoryRequest(payment.Id, "Entertainment"), ana, users, db, CancellationToken.None);
        var id = db.CategoryRules.Single().Id;

        var stranger = await RemoveCategoryRule.HandleAsync(id, Principal("ben@example.com"), users, db, CancellationToken.None);
        Assert.IsType<NotFound>(stranger.Result);
        Assert.Single(db.CategoryRules);

        var owner = await RemoveCategoryRule.HandleAsync(id, ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(owner.Result);
        Assert.Null((await ListAsync(db, ana)).Single().Category);
    }

    private static CategoryRow Row(Guid account, string bookedOn, decimal amount, string counterparty) =>
        new(account, DateOnly.Parse(bookedOn), amount, "CZK", counterparty, null);

    private static CategoryRule Rule(string category, string matchKey) =>
        new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Category = category, MatchKey = matchKey };

    private static async Task<List<TransactionResponse>> ListAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var listed = await ListTransactions.HandleAsync(
            null, null, null, null, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<TransactionResponse>>>(listed.Result).Value!;
    }

    private static Transaction Add(JointDbContext db, Account account, string bookedOn, decimal amount, string counterparty)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            ExternalId = Guid.NewGuid().ToString(),
            BookedOn = DateOnly.Parse(bookedOn),
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
