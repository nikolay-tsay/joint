using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Categories;
using Joint.Api.Features.Insights;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Transactions;

/// <summary>
/// Lists the household's transactions, newest first. Narrow them to one account with
/// <c>accountId</c> or to one kind of account with <c>kind</c> (Current, Savings,
/// Investment), and to payments in some categories by repeating <c>category</c> (any of
/// them matches) and adding <c>uncategorized=true</c> for payments with none. Page with
/// <c>skip</c> and <c>limit</c>.
/// </summary>
public class ListTransactions : IEndpoint
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/transactions", HandleAsync)
            .WithName("ListTransactions")
            .RequireAuthorization();

    public static async Task<Results<Ok<List<TransactionResponse>>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        Guid? accountId,
        string? kind,
        int? skip,
        int? limit,
        ClaimsPrincipal principal,
        CurrentUserService currentUser,
        JointDbContext db,
        CancellationToken cancellationToken,
        string[]? category = null,
        bool? uncategorized = null)
    {
        var user = await currentUser.GetOrCreateAsync(principal, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        AccountKind? accountKind = null;
        if (!string.IsNullOrEmpty(kind))
        {
            // Names only: Enum.TryParse would also accept a bare number.
            if (!Enum.GetNames<AccountKind>().Contains(kind, StringComparer.OrdinalIgnoreCase))
            {
                return TypedResults.Problem(
                    $"Kind must be one of: {string.Join(", ", Enum.GetNames<AccountKind>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            accountKind = Enum.Parse<AccountKind>(kind, ignoreCase: true);
        }

        // Joining through the household's accounts is what keeps other households' data out,
        // including when someone passes an account id that is not theirs.
        var query =
            from transaction in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on transaction.AccountId equals account.Id
            join connection in db.BankConnections on account.ConnectionId equals connection.Id
            join owner in db.Users on connection.OwnerUserId equals owner.Id
            where accountId == null || account.Id == accountId
            where accountKind == null || account.Kind == accountKind
            orderby transaction.BookedOn descending, transaction.Id
            select new TransactionResponse(
                transaction.Id,
                account.Id,
                account.Name,
                owner.DisplayName,
                owner.AvatarUrl,
                transaction.BookedOn,
                transaction.Amount,
                transaction.Currency,
                transaction.Counterparty,
                transaction.Description);

        var offset = Math.Max(skip ?? 0, 0);
        var count = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var wanted = Wanted(category, uncategorized);
        var transactions = wanted is null
            ? await query.Skip(offset).Take(count).ToListAsync(cancellationToken)
            : await InCategoriesAsync(query, wanted, user.HouseholdId, db, offset, count, cancellationToken);

        transactions = await WithTransfersAsync(transactions, user.HouseholdId, db, cancellationToken);
        return TypedResults.Ok(await WithRecurringAsync(transactions, user.HouseholdId, db, cancellationToken));
    }

    /// <summary>The category filter asked for, or null when the list is not narrowed by category.</summary>
    private static CategoryFilter? Wanted(string[]? categories, bool? uncategorized)
    {
        var names = (categories ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.Count == 0 && uncategorized != true ? null : new CategoryFilter(names, uncategorized == true);
    }

    /// <summary>
    /// One page of the payments in the wanted categories. A category is decided in code
    /// (a payee's name contains a rule's text), so the filtered list cannot be paged in the
    /// database: the payments of the household, a few thousand small rows, are matched in
    /// memory and the page is cut from what is left. Only payments have a category, so
    /// money coming in never passes this filter.
    /// </summary>
    private static async Task<List<TransactionResponse>> InCategoriesAsync(
        IQueryable<TransactionResponse> query,
        CategoryFilter wanted,
        Guid householdId,
        JointDbContext db,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var matcher = new CategoryMatcher(await db.CategoryRulesOf(householdId).ToListAsync(cancellationToken));
        var payments = await query.Where(t => t.Amount < 0).ToListAsync(cancellationToken);
        return payments
            .Where(t => matcher.Find(t.Counterparty, t.Description) is { } rule
                ? wanted.Names.Contains(rule.Category)
                : wanted.Uncategorized)
            .Skip(offset)
            .Take(count)
            .ToList();
    }

    private sealed record CategoryFilter(HashSet<string> Names, bool Uncategorized);

    /// <summary>
    /// Says which of the listed transactions the app recognises as money moved between the
    /// household's own accounts, so the user can see it is already left out of income and
    /// spending and does not mark it by hand. The other half of a transfer is usually in
    /// another account and not on this page, so the household's transactions of the same
    /// amounts around the page's dates are read to pair against.
    /// </summary>
    private static async Task<List<TransactionResponse>> WithTransfersAsync(
        List<TransactionResponse> transactions, Guid householdId, JointDbContext db, CancellationToken cancellationToken)
    {
        if (transactions.Count == 0)
        {
            return transactions;
        }

        // Twice the window: a neighbour just off the page may itself pair with something further out.
        var margin = 2 * InternalTransfers.WindowDays;
        var from = transactions.Min(t => t.BookedOn).AddDays(-margin);
        var to = transactions.Max(t => t.BookedOn).AddDays(margin);
        var amounts = transactions.SelectMany(t => new[] { t.Amount, -t.Amount }).Distinct().ToList();

        var candidates = await (
            from transaction in db.Transactions
            join account in db.AccountsOf(householdId) on transaction.AccountId equals account.Id
            where transaction.BookedOn >= @from && transaction.BookedOn <= to && amounts.Contains(transaction.Amount)
            select new TransferCandidate(
                transaction.Id, account.Id, transaction.BookedOn, transaction.Amount, transaction.Currency))
            .ToListAsync(cancellationToken);

        // Per currency, as the calculators do: amounts are never compared across currencies.
        var paired = candidates
            .GroupBy(c => c.Currency)
            .SelectMany(g => InternalTransfers.Find(g.ToList(), c => c.AccountId, c => c.BookedOn, c => c.Amount))
            .Select(c => c.Id)
            .ToHashSet();

        return transactions.Select(t => paired.Contains(t.Id) ? t with { IsInternalTransfer = true } : t).ToList();
    }

    private sealed record TransferCandidate(Guid Id, Guid AccountId, DateOnly BookedOn, decimal Amount, string Currency);

    /// <summary>
    /// Says which of the listed payments count towards a tracked subscription or regular
    /// payment and which have a category, and for the rest what text a rule would match on. Done on the page
    /// just read, because matching is decided in code.
    /// </summary>
    private static async Task<List<TransactionResponse>> WithRecurringAsync(
        List<TransactionResponse> transactions, Guid householdId, JointDbContext db, CancellationToken cancellationToken)
    {
        var matcher = new RecurringMatcher(await db.RecurringOf(householdId).ToListAsync(cancellationToken));
        var categories = new CategoryMatcher(await db.CategoryRulesOf(householdId).ToListAsync(cancellationToken));

        return transactions
            .Select(t =>
            {
                if (t.Amount >= 0)
                {
                    return t;
                }

                var suggestion = PaymentKey.From(t.Counterparty, t.Description);
                t = categories.Find(t.Counterparty, t.Description) is { } rule
                    ? t with { Category = rule.Category, CategoryRuleId = rule.Id, CategoryMatch = rule.MatchKey }
                    : t with { CategoryMatch = suggestion };

                return matcher.Find(t.Counterparty, t.Description) is { } recurring
                    ? t with
                    {
                        RecurringId = recurring.Id,
                        RecurringKind = recurring.Kind.ToString(),
                        RecurringMatch = recurring.MatchKey,
                    }
                    : t with { RecurringMatch = suggestion };
            })
            .ToList();
    }
}
