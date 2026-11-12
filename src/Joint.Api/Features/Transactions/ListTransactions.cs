using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Transactions;

/// <summary>
/// Lists the household's transactions, newest first, optionally for one account.
/// Page with <c>skip</c> and <c>limit</c>.
/// </summary>
public class ListTransactions : IEndpoint
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/transactions", HandleAsync)
            .WithName("ListTransactions")
            .RequireAuthorization();

    public static async Task<Results<Ok<List<TransactionResponse>>, UnauthorizedHttpResult>> HandleAsync(
        Guid? accountId,
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

        // Joining through the household's accounts is what keeps other households' data out,
        // including when someone passes an account id that is not theirs.
        var transactions = await (
            from transaction in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on transaction.AccountId equals account.Id
            where accountId == null || account.Id == accountId
            orderby transaction.BookedOn descending, transaction.Id
            select new TransactionResponse(
                transaction.Id,
                account.Id,
                account.Name,
                transaction.BookedOn,
                transaction.Amount,
                transaction.Currency,
                transaction.Counterparty,
                transaction.Description))
            .Skip(Math.Max(skip ?? 0, 0))
            .Take(Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(transactions);
    }
}
