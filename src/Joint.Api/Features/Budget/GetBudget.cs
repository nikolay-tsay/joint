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

namespace Joint.Api.Features.Budget;

/// <summary>
/// The household's monthly budget, its category lines, and how this month's spending stands
/// against them. The figures are returned whether or not a budget is set, so the app can suggest one.
/// </summary>
public class GetBudget : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/budget", HandleAsync)
            .WithName("GetBudget")
            .RequireAuthorization();

    public static async Task<Results<Ok<BudgetResponse>, UnauthorizedHttpResult>> HandleAsync(
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

        var limit = await db.Households
            .Where(h => h.Id == user.HouseholdId)
            .Select(h => h.MonthlyBudget)
            .FirstOrDefaultAsync(cancellationToken);

        // Two months: this one, and the last for comparison and for what is still expected.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var loaded = await (
            from transaction in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on transaction.AccountId equals account.Id
            join connection in db.BankConnections on account.ConnectionId equals connection.Id
            join owner in db.Users on connection.OwnerUserId equals owner.Id
            where transaction.BookedOn >= start
            select new
            {
                AccountId = account.Id,
                OwnerId = owner.Id,
                owner.DisplayName,
                owner.AvatarUrl,
                transaction.BookedOn,
                transaction.Amount,
                transaction.Currency,
                transaction.Counterparty,
                transaction.Description,
            })
            .ToListAsync(cancellationToken);

        // Money marked as moved to the household's own account is not spending at all. In the
        // flows it is only flagged, because the calculator pairs transfers up before dropping it.
        var matcher = new RecurringMatcher(await db.RecurringOf(user.HouseholdId).ToListAsync(cancellationToken));
        var flows = loaded
            .Select(t => new FlowRow(
                t.AccountId, t.OwnerId, t.DisplayName, t.AvatarUrl, t.BookedOn, t.Amount, t.Currency,
                MarkedAsTransfer: matcher.IsOwnTransfer(t.Amount, t.Counterparty, t.Description)))
            .ToList();

        // Nor is it a fixed cost, so it is kept out of the tracked payments altogether.
        var recurring = loaded
            .Where(t => t.Amount < 0 && !matcher.IsOwnTransfer(t.Amount, t.Counterparty, t.Description))
            .Select(t => new RecurringRow(
                t.BookedOn, t.Amount, t.Currency, matcher.Find(t.Counterparty, t.Description)?.MatchKey))
            .ToList();

        // The category lines: each payment's category, and whether it is a tracked fixed cost.
        var lines = await db.CategoryBudgetsOf(user.HouseholdId).ToListAsync(cancellationToken);
        var categorized = new List<CategorizedRow>();
        if (lines.Count > 0)
        {
            var categories = new CategoryMatcher(await db.CategoryRulesOf(user.HouseholdId).ToListAsync(cancellationToken));
            categorized = loaded
                .Select(t => new CategorizedRow(
                    t.AccountId,
                    t.BookedOn,
                    t.Amount,
                    t.Currency,
                    MarkedAsTransfer: matcher.IsOwnTransfer(t.Amount, t.Counterparty, t.Description),
                    Tracked: t.Amount < 0 && matcher.Find(t.Counterparty, t.Description) is { Kind: not RecurringKind.Transfer },
                    t.Amount < 0 ? categories.Find(t.Counterparty, t.Description)?.Category : null))
                .ToList();
        }

        return TypedResults.Ok(BudgetCalculator.Build(limit, flows, recurring, today, lines, categorized));
    }
}
