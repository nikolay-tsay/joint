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

public class TransferFilterTests
{
    [Fact]
    public async Task Hiding_transfers_drops_both_halves_even_when_the_other_half_is_not_listed()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var (everyday, savings) = await AccountsAsync(db, ana);
        var albert = Add(db, everyday, "2026-11-05", -450, "Albert");
        Add(db, everyday, "2026-11-04", -5000, "To savings");
        Add(db, savings, "2026-11-05", 5000, "From everyday");
        var salary = Add(db, everyday, "2026-11-01", 40000, "Employer");
        await db.SaveChangesAsync();

        var all = await ListAsync(db, ana, kind: "Current");
        var hidden = await ListAsync(db, ana, kind: "Current", hideTransfers: true);
        var savingsHidden = await ListAsync(db, ana, kind: "Savings", hideTransfers: true);
        var secondPage = await ListAsync(db, ana, hideTransfers: true, skip: 1, limit: 1);

        Assert.Equal(3, all.Count);
        Assert.Contains(all, t => t.IsInternalTransfer);
        Assert.Equal([albert.Id, salary.Id], hidden.Select(t => t.Id));
        Assert.Empty(savingsHidden);
        Assert.Equal([salary.Id], secondPage.Select(t => t.Id));
    }

    [Fact]
    public async Task Hiding_transfers_combines_with_a_category_filter()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var (everyday, savings) = await AccountsAsync(db, ana);
        var albert = Add(db, everyday, "2026-11-05", -450, "Albert");
        var toSavings = Add(db, everyday, "2026-11-04", -5000, "Ana Novak");
        Add(db, savings, "2026-11-04", 5000, "Ana Novak");
        await db.SaveChangesAsync();
        await AssignAsync(db, ana, albert, "Groceries");
        await AssignAsync(db, ana, toSavings, "Other");

        var listed = await ListAsync(db, ana, categories: ["Groceries", "Other"], hideTransfers: true);

        Assert.Equal([albert.Id], listed.Select(t => t.Id));
    }

    private static async Task AssignAsync(JointDbContext db, ClaimsPrincipal principal, Transaction payment, string category)
    {
        var result = await AssignCategory.HandleAsync(
            new AssignCategoryRequest(payment.Id, category), principal, new CurrentUserService(db), db, CancellationToken.None);
        Assert.IsType<NoContent>(result.Result);
    }

    private static async Task<List<TransactionResponse>> ListAsync(
        JointDbContext db, ClaimsPrincipal principal, string? kind = null, string[]? categories = null,
        bool hideTransfers = false, int? skip = null, int? limit = null)
    {
        var listed = await ListTransactions.HandleAsync(
            null, kind, skip, limit, principal, new CurrentUserService(db), db, CancellationToken.None,
            categories, null, hideTransfers);
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
