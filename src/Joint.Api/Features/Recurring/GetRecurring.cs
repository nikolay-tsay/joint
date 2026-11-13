using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Recurring;

/// <summary>
/// The household's subscriptions and regular payments: each with its latest payment, and
/// what both kinds cost per month. <c>months</c> sets how far back the monthly series goes.
/// </summary>
public class GetRecurring : IEndpoint
{
    public const int DefaultMonths = 6;

    /// <summary>
    /// Also how far back a payee's latest payment is looked for, so a yearly subscription
    /// still shows when it was last paid.
    /// </summary>
    public const int MaxMonths = 12;

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/recurring", HandleAsync)
            .WithName("GetRecurring")
            .RequireAuthorization();

    public static async Task<Results<Ok<RecurringOverviewResponse>, UnauthorizedHttpResult>> HandleAsync(
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var payments = await db.RecurringOf(user.HouseholdId).ToListAsync(cancellationToken);
        var span = Math.Clamp(months ?? DefaultMonths, 1, MaxMonths);
        if (payments.Count == 0)
        {
            return TypedResults.Ok(RecurringCalculator.Build([], [], today, span));
        }

        // The payee key is worked out in code, so matching happens in memory over a year
        // of the household's outgoing transactions: a few thousand small rows.
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(1 - MaxMonths);
        var outgoing = await (
            from transaction in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on transaction.AccountId equals account.Id
            where transaction.BookedOn >= start && transaction.Amount < 0
            select new { transaction.BookedOn, transaction.Amount, transaction.Currency, transaction.Counterparty, transaction.Description })
            .ToListAsync(cancellationToken);

        var rows = outgoing
            .Select(t => new RecurringRow(t.BookedOn, t.Amount, t.Currency, PaymentKey.From(t.Counterparty, t.Description)))
            .ToList();

        return TypedResults.Ok(RecurringCalculator.Build(payments, rows, today, span));
    }
}
