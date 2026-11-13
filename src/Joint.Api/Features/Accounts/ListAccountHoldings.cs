using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Accounts;

/// <summary>Lists what an investment account currently holds, largest position first.</summary>
public class ListAccountHoldings : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/accounts/{accountId:guid}/holdings", HandleAsync)
            .WithName("ListAccountHoldings")
            .RequireAuthorization();

    public static async Task<Results<Ok<List<HoldingResponse>>, UnauthorizedHttpResult, NotFound>> HandleAsync(
        Guid accountId,
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
        var account = await db.AccountsOf(user.HouseholdId)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        var holdings = await db.Holdings
            .Where(h => h.AccountId == accountId)
            .OrderByDescending(h => h.Value)
            .ThenBy(h => h.Name)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(holdings
            .Select(h => new HoldingResponse(
                h.Ticker,
                h.Name,
                h.Quantity,
                h.AveragePrice,
                h.CurrentPrice,
                h.Currency,
                h.Value,
                h.UnrealizedProfitLoss,
                account.Currency))
            .ToList());
    }
}
