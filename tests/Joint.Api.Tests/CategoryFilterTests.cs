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

public class CategoryFilterTests
{
    [Fact]
    public async Task The_list_can_be_narrowed_to_several_categories_at_once()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var albert = Add(db, account, "2026-11-03", -450, "Albert");
        var lidl = Add(db, account, "2026-11-02", -300, "Lidl");
        var pizza = Add(db, account, "2026-11-01", -250, "Pizza Nuova");
        Add(db, account, "2026-11-01", -900, "Alza");
        Add(db, account, "2026-11-01", 40000, "Albert Employer");
        await db.SaveChangesAsync();
        await AssignAsync(db, ana, albert, "Groceries");
        await AssignAsync(db, ana, lidl, "Groceries");
        await AssignAsync(db, ana, pizza, "Eating out");

        var listed = await ListAsync(db, ana, ["groceries", "Eating out"]);

        Assert.Equal([albert.Id, lidl.Id, pizza.Id], listed.Select(t => t.Id));
        Assert.Equal(["Groceries", "Groceries", "Eating out"], listed.Select(t => t.Category));
    }

    [Fact]
    public async Task Payments_with_no_category_can_be_listed_alone_or_with_categories_and_paged()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var albert = Add(db, account, "2026-11-05", -450, "Albert");
        var alza = Add(db, account, "2026-11-04", -900, "Alza");
        var shell = Add(db, account, "2026-11-03", -1200, "Shell");
        Add(db, account, "2026-11-02", 40000, "Employer");
        await db.SaveChangesAsync();
        await AssignAsync(db, ana, albert, "Groceries");

        var none = await ListAsync(db, ana, [], uncategorized: true);
        var both = await ListAsync(db, ana, ["Groceries"], uncategorized: true);
        var secondPage = await ListAsync(db, ana, ["Groceries"], uncategorized: true, skip: 1, limit: 1);

        Assert.Equal([alza.Id, shell.Id], none.Select(t => t.Id));
        Assert.Equal([albert.Id, alza.Id, shell.Id], both.Select(t => t.Id));
        Assert.Equal([alza.Id], secondPage.Select(t => t.Id));
    }

    [Fact]
    public async Task Removing_a_category_takes_it_off_every_payee_and_its_name_off_the_list()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var vet = Add(db, account, "2026-11-03", -800, "Vet Clinic");
        var petShop = Add(db, account, "2026-11-02", -300, "Pet Shop");
        var albert = Add(db, account, "2026-11-01", -450, "Albert");
        await db.SaveChangesAsync();
        await AssignAsync(db, ana, vet, "Pets");
        await AssignAsync(db, ana, petShop, "pets");
        await AssignAsync(db, ana, albert, "Groceries");
        Assert.Equal(2, (await CategoriesAsync(db, ana)).Payees!["Pets"]);

        var removed = await RemoveCategory.HandleAsync("PETS", ana, new CurrentUserService(db), db, CancellationToken.None);
        var again = await RemoveCategory.HandleAsync("Pets", ana, new CurrentUserService(db), db, CancellationToken.None);

        Assert.IsType<NoContent>(removed.Result);
        Assert.IsType<NotFound>(again.Result);
        var listed = await ListAsync(db, ana, []);
        Assert.Null(listed.Single(t => t.Id == vet.Id).Category);
        Assert.Null(listed.Single(t => t.Id == petShop.Id).Category);
        Assert.Equal("Groceries", listed.Single(t => t.Id == albert.Id).Category);
        var categories = await CategoriesAsync(db, ana);
        Assert.DoesNotContain("Pets", categories.Names);
        Assert.False(categories.Payees!.ContainsKey("Pets"));
    }

    [Fact]
    public async Task Deleting_a_starter_category_takes_it_off_the_list_until_it_is_used_again()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var toys = Add(db, account, "2026-11-02", -600, "Toy Shop");
        var nursery = Add(db, account, "2026-11-01", -900, "Nursery");
        await db.SaveChangesAsync();
        await AssignAsync(db, ana, toys, "Kids");
        var users = new CurrentUserService(db);

        var deleted = await RemoveCategory.HandleAsync("kids", ana, users, db, CancellationToken.None);
        var unused = await RemoveCategory.HandleAsync("Travel", ana, users, db, CancellationToken.None);
        var twice = await RemoveCategory.HandleAsync("Travel", ana, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(deleted.Result);
        Assert.IsType<NoContent>(unused.Result);
        Assert.IsType<NotFound>(twice.Result);
        Assert.Null((await ListAsync(db, ana, [])).Single(t => t.Id == toys.Id).Category);
        var names = (await CategoriesAsync(db, ana)).Names;
        Assert.DoesNotContain("Kids", names);
        Assert.DoesNotContain("Travel", names);
        Assert.Contains("Groceries", names);

        // Typed again in other capitals, it comes back with its usual spelling and stays.
        await AssignAsync(db, ana, nursery, "KIDS");
        await RemoveCategoryRule.HandleAsync(
            db.CategoryRules.Single().Id, ana, users, db, CancellationToken.None);
        Assert.Contains("Kids", (await CategoriesAsync(db, ana)).Names);
        Assert.DoesNotContain("Travel", (await CategoriesAsync(db, ana)).Names);
    }

    [Fact]
    public async Task A_category_can_only_be_removed_within_the_household()
    {
        await using var db = NewDb();
        var ana = Principal("ana@example.com");
        var account = await AccountAsync(db, ana);
        var albert = Add(db, account, "2026-11-01", -450, "Albert");
        await db.SaveChangesAsync();
        await AssignAsync(db, ana, albert, "Groceries");

        var ben = Principal("ben@example.com");
        var users = new CurrentUserService(db);

        // Ben deletes the starter from his own household's list; Ana's payee and list stay.
        var hidden = await RemoveCategory.HandleAsync("Groceries", ben, users, db, CancellationToken.None);
        var ownOfAna = await RemoveCategory.HandleAsync("Pets", ben, users, db, CancellationToken.None);

        Assert.IsType<NoContent>(hidden.Result);
        Assert.IsType<NotFound>(ownOfAna.Result);
        Assert.Single(db.CategoryRules);
        Assert.Contains("Groceries", (await CategoriesAsync(db, ana)).Names);
        Assert.DoesNotContain("Groceries", (await CategoriesAsync(db, ben)).Names);
    }

    private static async Task AssignAsync(JointDbContext db, ClaimsPrincipal principal, Transaction payment, string category)
    {
        var result = await AssignCategory.HandleAsync(
            new AssignCategoryRequest(payment.Id, category), principal, new CurrentUserService(db), db, CancellationToken.None);
        Assert.IsType<NoContent>(result.Result);
    }

    private static async Task<CategoriesResponse> CategoriesAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await GetCategories.HandleAsync(null, principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<CategoriesResponse>>(result.Result).Value!;
    }

    private static async Task<List<TransactionResponse>> ListAsync(
        JointDbContext db, ClaimsPrincipal principal, string[] categories, bool uncategorized = false, int? skip = null, int? limit = null)
    {
        var listed = await ListTransactions.HandleAsync(
            null, null, skip, limit, principal, new CurrentUserService(db), db, CancellationToken.None,
            categories, uncategorized);
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
