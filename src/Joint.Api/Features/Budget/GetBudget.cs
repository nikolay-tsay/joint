using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Features.Insights;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Budget;

/// <summary>
/// The household's monthly budget and how this month's spending stands against it. The
/// figures are returned whether or not a budget is set, so the app can suggest one.
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
        var transactions = await (
            from transaction in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on transaction.AccountId equals account.Id
            join connection in db.BankConnections on account.ConnectionId equals connection.Id
            join owner in db.Users on connection.OwnerUserId equals owner.Id
            // A payment taken out of the budget by hand is left out of every figure here.
            where transaction.BookedOn >= start && !transaction.ExcludedFromBudget
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

        var flows = transactions
            .Select(t => new FlowRow(t.AccountId, t.OwnerId, t.DisplayName, t.AvatarUrl, t.BookedOn, t.Amount, t.Currency))
            .ToList();

        var matcher = new RecurringMatcher(await db.RecurringOf(user.HouseholdId).ToListAsync(cancellationToken));
        var recurring = transactions
            .Where(t => t.Amount < 0)
            .Select(t => new RecurringRow(
                t.BookedOn, t.Amount, t.Currency, matcher.Find(t.Counterparty, t.Description)?.MatchKey))
            .ToList();

        return TypedResults.Ok(BudgetCalculator.Build(limit, flows, recurring, today));
    }
}
