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
    public async Task A_goal_kept_by_hand_reports_the_amount_it_was_given()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        await AccountAsync(db, ana, balance: 1000);
        var users = new CurrentUserService(db);

        var created = await CreateGoal.HandleAsync(
            new SaveGoalRequest(" Holiday ", 60000, null, 15000), ana, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(created.Result);
        var goal = Assert.Single(await ListAsync(db, ana));
        Assert.Equal(("Holiday", 60000m, 15000m, "CZK"), (goal.Name, goal.TargetAmount, goal.Saved, goal.Currency));
        Assert.Null(goal.AccountId);
    }

    [Fact]
    public async Task A_goal_following_an_account_reports_that_accounts_balance()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana, balance: 42000, currency: "EUR");
        var users = new CurrentUserService(db);

        await CreateGoal.HandleAsync(
            new SaveGoalRequest("Car", 100000, account.Id, 5), ana, users, db, CancellationToken.None);

        var goal = Assert.Single(await ListAsync(db, ana));
        Assert.Equal((42000m, "EUR", account.Id, "Savings"), (goal.Saved, goal.Currency, goal.AccountId, goal.AccountName));

        account.Balance = 43500;
        await db.SaveChangesAsync();
        Assert.Equal(43500m, Assert.Single(await ListAsync(db, ana)).Saved);
    }

    [Fact]
    public async Task A_goal_is_a_saving_goal_unless_it_says_investment()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var users = new CurrentUserService(db);

        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, null, 0), ana, users, db, CancellationToken.None);
        await CreateGoal.HandleAsync(
            new SaveGoalRequest("Portfolio", 1000000, null, 250000, "Investment"), ana, users, db, CancellationToken.None);
        var nonsense = await CreateGoal.HandleAsync(
            new SaveGoalRequest("Other", 5, null, 0, "1"), ana, users, db, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(nonsense.Result);
        Assert.Equal(
            [("Holiday", "Saving"), ("Portfolio", "Investment")],
            (await ListAsync(db, ana)).Select(g => (g.Name, g.Kind)));
    }

    [Fact]
    public async Task A_goal_can_be_changed_and_removed()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var users = new CurrentUserService(db);
        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, null, 0), ana, users, db, CancellationToken.None);
        var id = db.Goals.Single().Id;

        var updated = await UpdateGoal.HandleAsync(
            id, new SaveGoalRequest("Summer holiday", 80000, null, 20000), ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(updated.Result);
        var goal = Assert.Single(await ListAsync(db, ana));
        Assert.Equal(("Summer holiday", 80000m, 20000m), (goal.Name, goal.TargetAmount, goal.Saved));

        var deleted = await DeleteGoal.HandleAsync(id, ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(deleted.Result);
        Assert.Empty(await ListAsync(db, ana));
    }

    [Theory]
    [InlineData("", 1000, 0)]
    [InlineData("Holiday", 0, 0)]
    [InlineData("Holiday", -5, 0)]
    [InlineData("Holiday", 1000, -1)]
    public async Task A_goal_needs_a_name_a_target_above_zero_and_no_negative_savings(string name, int target, int saved)
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");

        var result = await CreateGoal.HandleAsync(
            new SaveGoalRequest(name, target, null, saved), ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Empty(db.Goals);
    }

    [Fact]
    public async Task Another_households_account_cannot_be_followed_and_its_goals_cannot_be_touched()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var ben = Principal("ben@example.com");
        var bensAccount = await AccountAsync(db, ben, balance: 9000);
        var users = new CurrentUserService(db);

        var following = await CreateGoal.HandleAsync(
            new SaveGoalRequest("Car", 100000, bensAccount.Id, null), ana, users, db, CancellationToken.None);
        Assert.IsType<ProblemHttpResult>(following.Result);

        await CreateGoal.HandleAsync(new SaveGoalRequest("Holiday", 60000, null, 0), ben, users, db, CancellationToken.None);
        var id = db.Goals.Single().Id;

        Assert.Empty(await ListAsync(db, ana));
        var changed = await UpdateGoal.HandleAsync(
            id, new SaveGoalRequest("Mine now", 1, null, 0), ana, users, db, CancellationToken.None);
        var removed = await DeleteGoal.HandleAsync(id, ana, users, db, CancellationToken.None);
        Assert.IsType<NotFound>(changed.Result);
        Assert.IsType<NotFound>(removed.Result);
        Assert.Equal("Holiday", db.Goals.Single().Name);
    }

    private static async Task<List<GoalResponse>> ListAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await ListGoals.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<GoalResponse>>>(result.Result).Value!;
    }

    private static async Task<Account> AccountAsync(
        JointDbContext db, ClaimsPrincipal principal, decimal balance, string currency = "CZK")
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
            Name = "Savings",
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
