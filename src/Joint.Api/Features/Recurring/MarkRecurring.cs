using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Recurring;

/// <summary>
/// Marks the payee of a transaction as a subscription or a regular payment. From then on
/// every payment to that payee counts, earlier ones included. Marking a payee that is
/// already tracked changes its kind.
/// </summary>
public class MarkRecurring : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/recurring", HandleAsync)
            .WithName("MarkRecurring")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound, ProblemHttpResult>> HandleAsync(
        MarkRecurringRequest request,
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

        // Names only: Enum.TryParse would also accept a bare number.
        if (!Enum.GetNames<RecurringKind>().Contains(request.Kind, StringComparer.OrdinalIgnoreCase)
            || !Enum.TryParse<RecurringKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return TypedResults.Problem(
                $"Kind must be one of: {string.Join(", ", Enum.GetNames<RecurringKind>())}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var transaction = await (
            from candidate in db.Transactions
            join account in db.AccountsOf(user.HouseholdId) on candidate.AccountId equals account.Id
            where candidate.Id == request.TransactionId
            select candidate)
            .FirstOrDefaultAsync(cancellationToken);
        if (transaction is null)
        {
            return TypedResults.NotFound();
        }

        var key = PaymentKey.From(transaction.Counterparty, transaction.Description);
        if (transaction.Amount >= 0 || key is null)
        {
            return TypedResults.Problem(
                "Only a payment that names who it went to can be tracked.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var existing = await db.RecurringOf(user.HouseholdId)
            .FirstOrDefaultAsync(r => r.MatchKey == key, cancellationToken);
        if (existing is not null)
        {
            existing.Kind = kind;
        }
        else
        {
            db.RecurringPayments.Add(new RecurringPayment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = kind,
                Name = PaymentKey.NameOf(transaction.Counterparty, transaction.Description)!,
                MatchKey = key,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
