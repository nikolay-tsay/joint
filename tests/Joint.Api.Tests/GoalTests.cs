using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Goals;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class GoalTests
{
    [Fact]
    public async Task A_saving_goal_counts_every_savings_account_and_nothing_else()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountAsync(db, ana, AccountKind.Savings, 30000);
        await AccountAsync(db, ana, AccountKind.Savings, 12000);
        await AccountAsync(db, ana, AccountKind.Current, 5000);
        await AccountAsync(db, ana, AccountKind.Investment, 250000);
        var users = new CurrentUserService(db);

        var created = await CreateGoal.HandleAsync(
            new SaveGoalRequest(" Holiday ", 60000, "Saving"), ana, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(created.Result);
        var goal = Assert.Single(await ListAsync(db, ana));
        Assert.Equal(
            ("Holiday", 60000m, 42000m, "CZK", "Saving"),
            (goal.Name, goal.TargetAmount, goal.Saved, goal.Currency, goal.Kind));
    }

    [Fact]
    public async Task An_investment_goal_counts_every_investment_account()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountAsync(db, ana, AccountKind.Savings, 30000);
        var portfolio = await AccountAsync(db, ana, AccountKind.Investment, 250000);
        await AccountAsync(db, ana, AccountKind.Investment, 40000);
        var users = new CurrentUserService(db);

        await CreateGoal.HandleAsync(
            new SaveGoalRequest("Portfolio", 1000000, "Investment"), ana, users, db, CancellationToken.None);
        Assert.Equal(290000m, Assert.Single(await ListAsync(db, ana)).Saved);

        // Progress is read from the balances, so it moves with them.
        portfolio.Balance = 260000;
        await db.SaveChangesAsync();
        Assert.Equal(300000m, Assert.Single(await ListAsync(db, ana)).Saved);
    }

    [Fact]
    public async Task Goals_of_one_kind_share_the_same_progress()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountAsync(db, ana, AccountKind.Savings, 42000);
        var users = new CurrentUserService(db);

        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, "Saving"), ana, users, db, CancellationToken.None);
        await CreateGoal.HandleAsync(new SaveGoalRequest("Reserve", 200000, "Saving"), ana, users, db, CancellationToken.None);

        Assert.Equal([42000m, 42000m], (await ListAsync(db, ana)).Select(g => g.Saved));
    }

    [Fact]
    public async Task Only_the_currency_most_of_those_accounts_are_in_is_added_up()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountAsync(db, ana, AccountKind.Savings, 30000);
        await AccountAsync(db, ana, AccountKind.Savings, 12000);
        await AccountAsync(db, ana, AccountKind.Savings, 900, "EUR");
        var users = new CurrentUserService(db);
        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, "Saving"), ana, users, db, CancellationToken.None);

        var goal = Assert.Single(await ListAsync(db, ana));

        Assert.Equal((42000m, "CZK"), (goal.Saved, goal.Currency));
    }

    [Fact]
    public async Task Without_an_account_of_the_type_a_goal_stands_at_zero()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountAsync(db, ana, AccountKind.Current, 5000);
        var users = new CurrentUserService(db);
        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, "Saving"), ana, users, db, CancellationToken.None);

        var goal = Assert.Single(await ListAsync(db, ana));

        Assert.Equal((0m, "CZK"), (goal.Saved, goal.Currency));
    }

    [Fact]
    public async Task A_goal_can_be_changed_and_removed()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var users = new CurrentUserService(db);
        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, "Saving"), ana, users, db, CancellationToken.None);
        var id = db.Goals.Single().Id;

        var updated = await UpdateGoal.HandleAsync(
            id, new SaveGoalRequest("Summer holiday", 80000, "Investment"), ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(updated.Result);
        var goal = Assert.Single(await ListAsync(db, ana));
        Assert.Equal(("Summer holiday", 80000m, "Investment"), (goal.Name, goal.TargetAmount, goal.Kind));

        var deleted = await DeleteGoal.HandleAsync(id, ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(deleted.Result);
        Assert.Empty(await ListAsync(db, ana));
    }

    [Theory]
    [InlineData("", 1000, "Saving")]
    [InlineData("Holiday", 0, "Saving")]
    [InlineData("Holiday", -5, "Saving")]
    [InlineData("Holiday", 1000, "1")]
    [InlineData("Holiday", 1000, "Pension")]
    public async Task A_goal_needs_a_name_a_target_above_zero_and_a_known_kind(string name, int target, string kind)
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");

        var result = await CreateGoal.HandleAsync(
            new SaveGoalRequest(name, target, kind), ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Empty(db.Goals);
    }

    [Fact]
    public async Task Another_households_goals_and_balances_stay_out_of_reach()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var ben = Principal("ben@example.com");
        await AccountAsync(db, ben, AccountKind.Savings, 9000);
        var users = new CurrentUserService(db);

        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, "Saving"), ben, users, db, CancellationToken.None);
        var id = db.Goals.Single().Id;

        // Ana sees neither Ben's goal nor his balance in a goal of her own.
        Assert.Empty(await ListAsync(db, ana));
        await CreateGoal.HandleAsync(new SaveGoalRequest("Car", 100000, "Saving"), ana, users, db, CancellationToken.None);
        Assert.Equal(0m, Assert.Single(await ListAsync(db, ana)).Saved);

        var changed = await UpdateGoal.HandleAsync(
            id, new SaveGoalRequest("Mine now", 1, "Saving"), ana, users, db, CancellationToken.None);
        var removed = await DeleteGoal.HandleAsync(id, ana, users, db, CancellationToken.None);
        Assert.IsType<NotFound>(changed.Result);
        Assert.IsType<NotFound>(removed.Result);
        Assert.Equal("Holiday", db.Goals.Single(g => g.Id == id).Name);
    }

    private static async Task<List<GoalResponse>> ListAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await ListGoals.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<GoalResponse>>>(result.Result).Value!;
    }

    private static async Task<Account> AccountAsync(
        JointDbContext db, ClaimsPrincipal principal, AccountKind kind, decimal balance, string currency = "CZK")
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
            ExternalId = Guid.NewGuid().ToString(),
            Name = kind.ToString(),
            Kind = kind,
            Currency = currency,
            Balance = balance,
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
