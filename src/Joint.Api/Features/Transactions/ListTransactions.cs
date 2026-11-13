using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Transactions;

/// <summary>
/// Lists the household's transactions, newest first. Narrow them to one account with
/// <c>accountId</c> or to one kind of account with <c>kind</c> (Current, Savings,
/// Investment). Page with <c>skip</c> and <c>limit</c>.
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
        CancellationToken cancellationToken)
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
        var transactions = await (
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
                transaction.Description))
            .Skip(Math.Max(skip ?? 0, 0))
            .Take(Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(await WithRecurringAsync(transactions, user.HouseholdId, db, cancellationToken));
    }

    /// <summary>
    /// Says which of the listed payments count towards a tracked subscription or regular
    /// payment. Done on the page just read, because the payee key is worked out in code.
    /// </summary>
    private static async Task<List<TransactionResponse>> WithRecurringAsync(
        List<TransactionResponse> transactions, Guid householdId, JointDbContext db, CancellationToken cancellationToken)
    {
        var tracked = (await db.RecurringOf(householdId).ToListAsync(cancellationToken))
            .GroupBy(r => r.MatchKey)
            .ToDictionary(g => g.Key, g => g.First());
        if (tracked.Count == 0)
        {
            return transactions;
        }

        return transactions
            .Select(t =>
                t.Amount < 0
                && PaymentKey.From(t.Counterparty, t.Description) is { } key
                && tracked.TryGetValue(key, out var recurring)
                    ? t with { RecurringId = recurring.Id, RecurringKind = recurring.Kind.ToString() }
                    : t)
            .ToList();
    }
}
