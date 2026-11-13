using System.Globalization;
using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Features.Insights;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Categories;

/// <summary>
/// What the household spent per category in one calendar month, and the category names it
/// can choose from. <c>month</c> is <c>yyyy-MM</c>; the current month when left out.
/// </summary>
public class GetCategories : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/categories", HandleAsync)
            .WithName("GetCategories")
            .RequireAuthorization();

    public static async Task<Results<Ok<CategoriesResponse>, UnauthorizedHttpResult, ProblemHttpResult>> HandleAsync(
        string? month,
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
        var first = new DateOnly(today.Year, today.Month, 1);
        if (!string.IsNullOrEmpty(month)
            && !DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out first))
        {
            return TypedResults.Problem("Month must look like 2026-11.", statusCode: StatusCodes.Status400BadRequest);
        }

        // A few days either side, so a transfer straddling the month's edge is still paired up.
        var from = first.AddDays(-InternalTransfers.WindowDays);
        var to = first.AddMonths(1).AddDays(InternalTransfers.WindowDays);
        var loaded = await (
            from transaction in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on transaction.AccountId equals account.Id
            where transaction.BookedOn >= @from && transaction.BookedOn < to
            select new CategoryRow(
                account.Id, transaction.BookedOn, transaction.Amount, transaction.Currency,
                transaction.Counterparty, transaction.Description))
            .ToListAsync(cancellationToken);

        // Same as Insights: what was marked as a transfer to the household's own account is not spending.
        var transfers = new RecurringMatcher(await db.RecurringOf(user.HouseholdId).ToListAsync(cancellationToken));
        // Only flagged here: the calculator pairs transfers up before dropping it.
        var rows = loaded
            .Select(r => r with { MarkedAsTransfer = transfers.IsOwnTransfer(r.Amount, r.Counterparty, r.Description) })
            .ToList();

        var rules = await db.CategoryRulesOf(user.HouseholdId).ToListAsync(cancellationToken);
        return TypedResults.Ok(CategoryCalculator.Build(rows, rules, first));
    }
}
