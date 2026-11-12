using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Accounts;

/// <summary>
/// Lists an account's transactions, newest first. Page backwards by passing the oldest
/// date received as <c>before</c>.
/// </summary>
public class ListAccountTransactions : IEndpoint
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/accounts/{accountId:guid}/transactions", HandleAsync)
            .WithName("ListAccountTransactions")
            .RequireAuthorization();

    public static async Task<Results<Ok<List<TransactionResponse>>, UnauthorizedHttpResult, NotFound>> HandleAsync(
        Guid accountId,
        DateOnly? before,
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

        // Going through the household query is what stops reading an outsider's account.
        if (!await db.AccountsOf(user.HouseholdId).AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var transactions = await db.Transactions
            .Where(t => t.AccountId == accountId && (before == null || t.BookedOn < before))
            .OrderByDescending(t => t.BookedOn)
            .ThenBy(t => t.ExternalId)
            .Take(take)
            .Select(t => new TransactionResponse(
                t.Id, t.BookedOn, t.Amount, t.Currency, t.Counterparty, t.Description))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(transactions);
    }
}
