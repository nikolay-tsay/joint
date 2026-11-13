using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Insights;

/// <summary>
/// The household's income and spending by month, this month's spending against last
/// month's, and who spent what. <c>months</c> sets how far back the monthly series goes.
/// </summary>
public class GetInsights : IEndpoint
{
    public const int DefaultMonths = 6;
    public const int MaxMonths = 12;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/insights", HandleAsync)
            .WithName("GetInsights")
            .RequireAuthorization();

    public static async Task<Results<Ok<InsightsResponse>, UnauthorizedHttpResult>> HandleAsync(
        int? months,
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

        // Always at least two months: the current one is compared with the one before.
        var span = Math.Clamp(months ?? DefaultMonths, 2, MaxMonths);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(1 - span);

        // A year of one household's transactions is a few thousand small rows, so the
        // arithmetic is done in memory where it is easy to test.
        var rows = await (
            from transaction in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on transaction.AccountId equals account.Id
            join connection in db.BankConnections on account.ConnectionId equals connection.Id
            join owner in db.Users on connection.OwnerUserId equals owner.Id
            where transaction.BookedOn >= start
            select new
            {
                Flow = new FlowRow(
                    account.Id, owner.Id, owner.DisplayName, owner.AvatarUrl,
                    transaction.BookedOn, transaction.Amount, transaction.Currency),
                transaction.Counterparty,
                transaction.Description,
            })
            .ToListAsync(cancellationToken);

        // A payment the household marked as a transfer to its own account is not spending.
        var matcher = new RecurringMatcher(await db.RecurringOf(user.HouseholdId).ToListAsync(cancellationToken));
        var flows = rows
            .Where(r => !matcher.IsOwnTransfer(r.Flow.Amount, r.Counterparty, r.Description))
            .Select(r => r.Flow)
            .ToList();

        return TypedResults.Ok(InsightsCalculator.Build(flows, today, span));
    }
}
