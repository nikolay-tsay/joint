using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Recurring;
using Joint.Api.Features.Transactions;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class RecurringTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    [Theory]
    [InlineData("Netflix", "Card payment", "NETFLIX")]
    [InlineData("  Novák,  Pavel ", null, "NOVÁK, PAVEL")]
    [InlineData(null, "Nákup: NETFLIX.COM, Los Gatos, dne 3.11.2026, částka 259.00 CZK", "NETFLIX.COM")]
    [InlineData(null, "Nákup: NETFLIX.COM, Los Gatos, dne 3.10.2026, částka 259.00 CZK", "NETFLIX.COM")]
    [InlineData(null, "Rent November", "RENT NOVEMBER")]
    [InlineData(null, "Spotify P2F3A9C1", "SPOTIFY")]
    [InlineData("ALBERT 0123", null, "ALBERT")]
    [InlineData("O2 Czech Republic", null, "O2 CZECH REPUBLIC")]
    [InlineData("Trading 212", null, "TRADING 212")]
    [InlineData("20261103", null, "20261103")]
    [InlineData(" ", "", null)]
    public void The_suggested_match_text_is_the_part_that_stays_the_same_between_payments(
        string? counterparty, string? description, string? expected) =>
        Assert.Equal(expected, PaymentKey.From(counterparty, description));

    [Fact]
    public void Monthly_totals_split_subscriptions_from_regular_payments()
    {
        var netflix = Payment(RecurringKind.Subscription, "Netflix");
        var rent = Payment(RecurringKind.Bill, "Landlord");

        var overview = RecurringCalculator.Build(
            [netflix, rent],
            [
                new RecurringRow(new DateOnly(2026, 10, 3), -259, "CZK", "NETFLIX"),
                new RecurringRow(new DateOnly(2026, 11, 3), -289, "CZK", "NETFLIX"),
                new RecurringRow(new DateOnly(2026, 11, 1), -18000, "CZK", "LANDLORD"),
                new RecurringRow(new DateOnly(2026, 11, 5), -450, "CZK", "ALBERT"),
                new RecurringRow(new DateOnly(2026, 11, 6), 289, "CZK", "NETFLIX"),
            ],
            Today, months: 2);

        Assert.Equal("CZK", overview.Currency);
        Assert.Equal((259m, 0m), (overview.Months[0].Subscriptions, overview.Months[0].Bills));
        Assert.Equal((289m, 18000m), (overview.Months[1].Subscriptions, overview.Months[1].Bills));
    }

    [Fact]
    public void Each_payment_lists_what_was_last_paid_and_when()
    {
        var netflix = Payment(RecurringKind.Subscription, "Netflix");
        var gym = Payment(RecurringKind.Subscription, "Gym");

        var overview = RecurringCalculator.Build(
            [netflix, gym],
            [
                new RecurringRow(new DateOnly(2026, 10, 3), -259, "CZK", "NETFLIX"),
                new RecurringRow(new DateOnly(2026, 11, 3), -289, "CZK", "NETFLIX"),
            ],
            Today, months: 2);

        var listed = overview.Payments.Single(p => p.Name == "Netflix");
        Assert.Equal((289m, "CZK", new DateOnly(2026, 11, 3)), (listed.LastAmount, listed.Currency, listed.LastPaidOn));

        var unpaid = overview.Payments.Single(p => p.Name == "Gym");
        Assert.Null(unpaid.LastAmount);
        Assert.Null(unpaid.LastPaidOn);
    }

    [Fact]
    public async Task Marking_a_payment_tracks_its_payee_and_shows_on_every_payment_to_it()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var october = Add(db, account, "2026-10-03", -259, "Netflix");
        var november = Add(db, account, "2026-11-03", -289, "Netflix");
        Add(db, account, "2026-11-05", -450, "Albert");
        await db.SaveChangesAsync();

        var marked = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(november.Id, "Subscription"), ana, new CurrentUserService(db), db, CancellationToken.None);
        Assert.IsType<NoContent>(marked.Result);

        var listed = await ListTransactions.HandleAsync(
            null, null, null, null, ana, new CurrentUserService(db), db, CancellationToken.None);
        var transactions = Assert.IsType<Ok<List<TransactionResponse>>>(listed.Result).Value!;
        Assert.Equal("Subscription", transactions.Single(t => t.Id == october.Id).RecurringKind);
        Assert.Equal("Subscription", transactions.Single(t => t.Id == november.Id).RecurringKind);
        Assert.Null(transactions.Single(t => t.Counterparty == "Albert").RecurringKind);
    }

    [Fact]
    public async Task A_payment_reference_in_the_description_does_not_stop_payments_matching()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var october = Add(db, account, "2026-10-03", -169, null, "Spotify P1A2B3C4D5");
        var november = Add(db, account, "2026-11-03", -169, null, "Spotify P9Z8Y7X6W5");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(november.Id, "Subscription"), ana, users, db, CancellationToken.None);

        Assert.Equal("SPOTIFY", db.RecurringPayments.Single().MatchKey);
        Assert.Equal("Spotify", db.RecurringPayments.Single().Name);
        var transactions = await ListAsync(db, ana);
        Assert.All(transactions, t => Assert.Equal("Subscription", t.RecurringKind));
        Assert.Contains(transactions, t => t.Id == october.Id);
    }

    [Fact]
    public async Task The_match_text_can_be_set_by_hand_and_must_appear_in_the_payment()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var rent = Add(db, account, "2026-11-01", -18000, null, "Najem 11/2026 byt Vinohrady");
        var earlier = Add(db, account, "2026-10-01", -18000, null, "Najem 10/2026 byt Vinohrady");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        var typo = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(rent.Id, "Bill", "Vinohrdy"), ana, users, db, CancellationToken.None);
        var tooShort = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(rent.Id, "Bill", "by"), ana, users, db, CancellationToken.None);
        Assert.IsType<ProblemHttpResult>(typo.Result);
        Assert.IsType<ProblemHttpResult>(tooShort.Result);
        Assert.Empty(db.RecurringPayments);

        var marked = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(rent.Id, "Bill", " byt vinohrady "), ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(marked.Result);
        Assert.Equal("BYT VINOHRADY", db.RecurringPayments.Single().MatchKey);

        var transactions = await ListAsync(db, ana);
        Assert.Equal("Bill", transactions.Single(t => t.Id == earlier.Id).RecurringKind);
        Assert.Equal("BYT VINOHRADY", transactions.Single(t => t.Id == earlier.Id).RecurringMatch);
    }

    [Fact]
    public async Task Marking_a_tracked_payment_again_with_other_text_changes_what_it_matches()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var payment = Add(db, account, "2026-11-03", -169, null, "Spotify P9Z8Y7X6W5");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(payment.Id, "Subscription", "Spotify P9Z8Y7X6W5"), ana, users, db, CancellationToken.None);
        await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(payment.Id, "Subscription", "Spotify"), ana, users, db, CancellationToken.None);

        Assert.Equal("SPOTIFY", Assert.Single(db.RecurringPayments).MatchKey);
    }

    [Fact]
    public void The_longest_match_text_wins_when_several_fit()
    {
        var apple = Payment(RecurringKind.Subscription, "Apple");
        var music = Payment(RecurringKind.Subscription, "Apple Music");
        var matcher = new RecurringMatcher([apple, music]);

        Assert.Same(music, matcher.Find("APPLE MUSIC", null));
        Assert.Same(apple, matcher.Find(null, "Apple.com/bill"));
        Assert.Null(matcher.Find("Albert", "Groceries"));
    }

    [Fact]
    public async Task An_untracked_payment_comes_with_a_suggested_match_text()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        Add(db, account, "2026-11-03", -169, null, "Spotify P9Z8Y7X6W5");
        Add(db, account, "2026-11-10", 40000, "Employer");
        await db.SaveChangesAsync();

        var transactions = await ListAsync(db, ana);

        Assert.Equal("SPOTIFY", transactions.Single(t => t.Amount < 0).RecurringMatch);
        Assert.Null(transactions.Single(t => t.Amount > 0).RecurringMatch);
    }

    [Fact]
    public async Task Marking_a_tracked_payee_again_changes_its_kind_instead_of_adding_another()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var rent = Add(db, account, "2026-11-01", -18000, "Landlord");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        await MarkRecurring.HandleAsync(new MarkRecurringRequest(rent.Id, "Subscription"), ana, users, db, CancellationToken.None);
        await MarkRecurring.HandleAsync(new MarkRecurringRequest(rent.Id, "Bill"), ana, users, db, CancellationToken.None);

        Assert.Equal(RecurringKind.Bill, Assert.Single(db.RecurringPayments).Kind);
    }

    [Fact]
    public async Task Income_and_other_households_transactions_cannot_be_marked()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var salary = Add(db, account, "2026-11-10", 40000, "Employer");
        var payment = Add(db, account, "2026-11-03", -289, "Netflix");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);

        var income = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(salary.Id, "Bill"), ana, users, db, CancellationToken.None);
        var stranger = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(payment.Id, "Subscription"), Principal("ben@example.com"), users, db, CancellationToken.None);
        var nonsense = await MarkRecurring.HandleAsync(
            new MarkRecurringRequest(payment.Id, "1"), ana, users, db, CancellationToken.None);

        Assert.IsType<ProblemHttpResult>(income.Result);
        Assert.IsType<NotFound>(stranger.Result);
        Assert.IsType<ProblemHttpResult>(nonsense.Result);
        Assert.Empty(db.RecurringPayments);
    }

    [Fact]
    public async Task Removing_stops_tracking_but_only_within_the_household()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var payment = Add(db, account, "2026-11-03", -289, "Netflix");
        await db.SaveChangesAsync();
        var users = new CurrentUserService(db);
        await MarkRecurring.HandleAsync(new MarkRecurringRequest(payment.Id, "Subscription"), ana, users, db, CancellationToken.None);
        var id = db.RecurringPayments.Single().Id;

        var stranger = await RemoveRecurring.HandleAsync(id, Principal("ben@example.com"), users, db, CancellationToken.None);
        Assert.IsType<NotFound>(stranger.Result);
        Assert.Single(db.RecurringPayments);

        var owner = await RemoveRecurring.HandleAsync(id, ana, users, db, CancellationToken.None);
        Assert.IsType<NoContent>(owner.Result);
        Assert.Empty(db.RecurringPayments);
    }

    private static RecurringPayment Payment(RecurringKind kind, string name) =>
        new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Kind = kind, Name = name, MatchKey = name.ToUpperInvariant() };

    private static async Task<List<TransactionResponse>> ListAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var listed = await ListTransactions.HandleAsync(
            null, null, null, null, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<List<TransactionResponse>>>(listed.Result).Value!;
    }

    private static Transaction Add(
        JointDbContext db, Account account, string bookedOn, decimal amount, string? counterparty, string? description = null)
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
            Description = description,
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
