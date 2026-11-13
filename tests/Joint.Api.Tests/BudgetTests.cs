using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Features.Budget;
using Joint.Api.Features.Insights;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class BudgetTests
{
    // The 10th of a 30-day month: a third of the way through.
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid Savings = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Spent_is_this_months_everyday_spending_without_internal_transfers()
    {
        var budget = BudgetCalculator.Build(
            40000,
            [
                Flow(Account, "2026-10-12", -9000),
                Flow(Account, "2026-11-02", -1500),
                Flow(Account, "2026-11-05", -5000),
                Flow(Savings, "2026-11-05", 5000),
                Flow(Account, "2026-11-08", 42000),
            ],
            [],
            Today);

        Assert.Equal("CZK", budget.Currency);
        Assert.Equal(40000m, budget.Limit);
        Assert.Equal(1500m, budget.Spent);
        Assert.Equal(9000m, budget.LastMonthSpent);
        Assert.Equal(new DateOnly(2026, 11, 1), budget.Month);
    }

    [Fact]
    public void Subscriptions_and_regular_payments_stay_out_of_the_budget()
    {
        // Rent was paid on the 1st; 3000 of everyday spending in ten days is 9000 by the 30th.
        var budget = BudgetCalculator.Build(
            null,
            [
                Flow(Account, "2026-11-01", -18000),
                Flow(Account, "2026-11-04", -3000),
            ],
            [
                Tracked("2026-11-01", -18000, "LANDLORD"),
                Tracked("2026-11-04", -3000, null),
            ],
            Today);

        Assert.Equal(3000m, budget.Spent);
        Assert.Equal(18000m, budget.FixedPaid);
        Assert.Equal(0m, budget.FixedExpected);
        Assert.Equal(9000m, budget.Projected);
    }

    [Fact]
    public void A_payment_made_last_month_and_not_yet_this_month_is_still_expected()
    {
        var budget = BudgetCalculator.Build(
            null,
            [
                Flow(Account, "2026-10-20", -289),
                Flow(Account, "2026-10-01", -18000),
                Flow(Account, "2026-10-15", -700),
                Flow(Account, "2026-11-01", -18000),
            ],
            [
                Tracked("2026-10-20", -289, "NETFLIX"),
                Tracked("2026-10-01", -18000, "LANDLORD"),
                Tracked("2026-11-01", -18000, "LANDLORD"),
            ],
            Today);

        Assert.Equal(0m, budget.Spent);
        Assert.Equal(18000m, budget.FixedPaid);
        Assert.Equal(289m, budget.FixedExpected);
        Assert.Equal(0m, budget.Projected);

        // Last month for comparison is everyday spending too, without its rent and Netflix.
        Assert.Equal(700m, budget.LastMonthSpent);
    }

    [Fact]
    public void A_household_without_transactions_has_nothing_to_show()
    {
        var budget = BudgetCalculator.Build(null, [], [], Today);

        Assert.Null(budget.Currency);
        Assert.Equal((0m, 0m), (budget.Spent, budget.Projected));
    }

    [Fact]
    public async Task A_budget_can_be_set_read_back_and_removed()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var users = new CurrentUserService(db);

        var set = await SetBudget.HandleAsync(new SetBudgetRequest(40000), ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(set.Result);
        Assert.Equal(40000m, await ReadLimitAsync(db, ana));

        await SetBudget.HandleAsync(new SetBudgetRequest(null), ana, users, db, CancellationToken.None);
        Assert.Null(await ReadLimitAsync(db, ana));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task A_budget_must_be_above_zero(int limit)
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");

        var result = await SetBudget.HandleAsync(
            new SetBudgetRequest(limit), ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Null(await ReadLimitAsync(db, ana));
    }

    [Fact]
    public async Task One_households_budget_is_not_anothers()
    {
        await using var db = NewDb();
        var users = new CurrentUserService(db);
        await SetBudget.HandleAsync(
            new SetBudgetRequest(40000), Principal("ana@example.com"), users, db, CancellationToken.None);

        Assert.Null(await ReadLimitAsync(db, Principal("ben@example.com")));
    }

    private static async Task<decimal?> ReadLimitAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await GetBudget.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<BudgetResponse>>(result.Result).Value!.Limit;
    }

    private static FlowRow Flow(Guid account, string bookedOn, decimal amount) =>
        new(account, Owner, "Ana", null, DateOnly.Parse(bookedOn), amount, "CZK");

    private static RecurringRow Tracked(string bookedOn, decimal amount, string? key) =>
        new(DateOnly.Parse(bookedOn), amount, "CZK", key);

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(string email) =>
        new(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", email)], "test"));
}
