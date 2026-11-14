using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Budget;
using Joint.Api.Features.Categories;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class CategoryBudgetTests
{
    // The budget works on the real current month.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task A_category_line_counts_everyday_spending_in_it_only()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var (everyday, savings) = await AccountsAsync(db, ana);
        var albert = Add(db, everyday, -450, "Albert");
        Add(db, everyday, -300, "Albert");
        var netflix = Add(db, everyday, -299, "Netflix");
        var toSavings = Add(db, everyday, -5000, "Ana Novak");
        Add(db, savings, 5000, "Ana Novak");
        Add(db, everyday, -800, "Alza");
        await db.SaveChangesAsync();
        await AssignAsync(db, ana, albert, "Groceries");
        await AssignAsync(db, ana, netflix, "Entertainment");
        await AssignAsync(db, ana, toSavings, "Other");
        await MarkAsync(db, ana, netflix, "Subscription");
        await SetBudgetAsync(db, ana, 20000);
        await SetLineAsync(db, ana, "groceries", 6000);
        await SetLineAsync(db, ana, "Entertainment", 1000);
        await SetLineAsync(db, ana, "Other", 500);

        var budget = await BudgetAsync(db, ana);

        // Netflix is a tracked subscription and the savings move a transfer: neither is everyday spending.
        Assert.Equal(1550m, budget.Spent);
        Assert.Equal(
            [("Entertainment", 1000m, 0m), ("Groceries", 6000m, 750m), ("Other", 500m, 0m)],
            budget.Categories!.Select(c => (c.Name, c.Limit, c.Spent)));
    }

    [Fact]
    public async Task Category_lines_must_fit_within_the_budget()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountsAsync(db, ana);

        Assert.Equal("Set a monthly budget first.", await RefusedAsync(db, ana, "Groceries", 1000));

        await SetBudgetAsync(db, ana, 10000);
        await SetLineAsync(db, ana, "Groceries", 6000);
        await SetLineAsync(db, ana, "Transport", 3000);

        Assert.Equal("Only 1000 of the budget is left for categories.", await RefusedAsync(db, ana, "Eating out", 1500));
        Assert.Equal("That is not one of your categories.", await RefusedAsync(db, ana, "Yachts", 100));
        Assert.Equal("A category's part of the budget is an amount above zero.", await RefusedAsync(db, ana, "Travel", 0));

        // Changing a line counts the room without its own old amount.
        await SetLineAsync(db, ana, "GROCERIES", 7000);
        Assert.Equal([("Groceries", 7000m), ("Transport", 3000m)], (await BudgetAsync(db, ana)).Categories!.Select(c => (c.Name, c.Limit)));
    }

    [Fact]
    public async Task Lowering_the_budget_shrinks_the_lines_and_removing_it_removes_them()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountsAsync(db, ana);
        await SetBudgetAsync(db, ana, 10000);
        await SetLineAsync(db, ana, "Groceries", 6000);
        await SetLineAsync(db, ana, "Transport", 3000);

        await SetBudgetAsync(db, ana, 12000);
        Assert.Equal([6000m, 3000m], (await BudgetAsync(db, ana)).Categories!.Select(c => c.Limit));

        await SetBudgetAsync(db, ana, 6000);
        Assert.Equal([4000m, 2000m], (await BudgetAsync(db, ana)).Categories!.Select(c => c.Limit));

        await SetBudgetAsync(db, ana, 1000);
        var shrunk = (await BudgetAsync(db, ana)).Categories!.Select(c => c.Limit).ToList();
        Assert.Equal([666.66m, 333.33m], shrunk);

        await SetBudgetAsync(db, ana, null);
        Assert.Empty((await BudgetAsync(db, ana)).Categories!);
        Assert.Empty(db.CategoryBudgets);
    }

    [Fact]
    public async Task A_line_can_be_taken_out_and_goes_with_its_category()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountsAsync(db, ana);
        await SetBudgetAsync(db, ana, 10000);
        await SetLineAsync(db, ana, "Groceries", 6000);
        await SetLineAsync(db, ana, "Transport", 3000);
        var users = new CurrentUserService(db);

        var removed = await RemoveCategoryBudget.HandleAsync("groceries", ana, users, db, CancellationToken.None);
        var again = await RemoveCategoryBudget.HandleAsync("Groceries", ana, users, db, CancellationToken.None);
        await RemoveCategory.HandleAsync("Transport", ana, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(removed.Result);
        Assert.IsType<NotFound>(again.Result);
        Assert.Empty((await BudgetAsync(db, ana)).Categories!);
    }

    [Fact]
    public async Task Lines_belong_to_the_household()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var ben = Principal("ben@example.com");
        await AccountsAsync(db, ana);
        await AccountsAsync(db, ben);
        await SetBudgetAsync(db, ana, 10000);
        await SetLineAsync(db, ana, "Groceries", 6000);

        var fromBen = await RemoveCategoryBudget.HandleAsync("Groceries", ben, new CurrentUserService(db), db, CancellationToken.None);

        Assert.IsType<NotFound>(fromBen.Result);
        Assert.Empty((await BudgetAsync(db, ben)).Categories!);
        Assert.Single((await BudgetAsync(db, ana)).Categories!);
    }

    [Fact]
    public async Task A_budget_can_be_made_with_its_categories_in_one_go()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountsAsync(db, ana);
        var users = new CurrentUserService(db);

        var tooMuch = await SetBudget.HandleAsync(
            new SetBudgetRequest(5000, [new("Groceries", 4000), new("Transport", 2000)]), ana, users, db, CancellationToken.None);
        var unknown = await SetBudget.HandleAsync(
            new SetBudgetRequest(5000, [new("Yachts", 100)]), ana, users, db, CancellationToken.None);
        var twice = await SetBudget.HandleAsync(
            new SetBudgetRequest(5000, [new("Groceries", 100), new("groceries", 100)]), ana, users, db, CancellationToken.None);

        Assert.Equal("The categories add up to more than the budget.", Assert.IsType<ProblemHttpResult>(tooMuch.Result).ProblemDetails.Detail);
        Assert.IsType<ProblemHttpResult>(unknown.Result);
        Assert.IsType<ProblemHttpResult>(twice.Result);
        Assert.Null((await BudgetAsync(db, ana)).Limit);

        var made = await SetBudget.HandleAsync(
            new SetBudgetRequest(5000, [new("groceries", 3000), new("Transport", 2000)]), ana, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(made.Result);
        var budget = await BudgetAsync(db, ana);
        Assert.Equal(5000m, budget.Limit);
        Assert.Equal([("Groceries", 3000m), ("Transport", 2000m)], budget.Categories!.Select(c => (c.Name, c.Limit)));
    }

    [Fact]
    public void Shrinking_rounds_down_so_the_lines_never_pass_the_budget()
    {
        List<CategoryBudget> lines =
        [
            new() { Category = "A", Amount = 100 },
            new() { Category = "B", Amount = 100 },
            new() { Category = "C", Amount = 100 },
        ];

        BudgetLines.ShrinkTo(100, lines);

        Assert.All(lines, l => Assert.Equal(33.33m, l.Amount));
        Assert.True(lines.Sum(l => l.Amount) <= 100);
    }

    private static async Task<string?> RefusedAsync(JointDbContext db, ClaimsPrincipal principal, string category, decimal amount)
    {
        var result = await SetCategoryBudget.HandleAsync(
            new SetCategoryBudgetRequest(category, amount), principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<ProblemHttpResult>(result.Result).ProblemDetails.Detail;
    }

    private static async Task SetLineAsync(JointDbContext db, ClaimsPrincipal principal, string category, decimal amount)
    {
        var result = await SetCategoryBudget.HandleAsync(
            new SetCategoryBudgetRequest(category, amount), principal, new CurrentUserService(db), db, CancellationToken.None);
        Assert.IsType<NoContent>(result.Result);
    }

    private static async Task SetBudgetAsync(JointDbContext db, ClaimsPrincipal principal, decimal? limit)
    {
        var result = await SetBudget.HandleAsync(new SetBudgetRequest(limit), principal, new CurrentUserService(db), db, CancellationToken.None);
        Assert.IsType<NoContent>(result.Result);
    }

    private static async Task<BudgetResponse> BudgetAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await GetBudget.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<BudgetResponse>>(result.Result).Value!;
    }

    private static async Task AssignAsync(JointDbContext db, ClaimsPrincipal principal, Transaction payment, string category)
    {
        var result = await AssignCategory.HandleAsync(
            new AssignCategoryRequest(payment.Id, category), principal, new CurrentUserService(db), db, CancellationToken.None);
        Assert.IsType<NoContent>(result.Result);
    }

    private static async Task MarkAsync(JointDbContext db, ClaimsPrincipal principal, Transaction payment, string kind)
    {
        var result = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(payment.Id, kind), principal, new CurrentUserService(db), db, CancellationToken.None);
        Assert.IsType<NoContent>(result.Result);
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

    private static async Task<(Account Everyday, Account Savings)> AccountsAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var user = (await new CurrentUserService(db).GetOrCreateAsync(principal, CancellationToken.None))!;
        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Provider = ProviderKind.LunchFlow,
            EncryptedCredential = "x",
        };
        Account New(string externalId, string name, AccountKind kind) => new()
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            ExternalId = externalId,
            Name = name,
            Currency = "CZK",
            Kind = kind,
        };
        var everyday = New("1", "Everyday", AccountKind.Current);
        var savings = New("2", "Savings", AccountKind.Savings);
        db.BankConnections.Add(connection);
        db.Accounts.AddRange(everyday, savings);
        await db.SaveChangesAsync();
        return (everyday, savings);
    }

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(string email) =>
        new(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", email)], "test"));
}
