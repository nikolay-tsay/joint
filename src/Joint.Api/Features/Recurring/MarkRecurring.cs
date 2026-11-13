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
/// every payment containing the match text counts, earlier ones included. The text is
/// suggested from the transaction and may be corrected by the caller. Marking a payment
/// that is already tracked changes that tracked payment's kind and match text.
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
            return Invalid($"Kind must be one of: {string.Join(", ", Enum.GetNames<RecurringKind>())}.");
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

        var name = PaymentKey.NameOf(transaction.Counterparty, transaction.Description);
        if (transaction.Amount >= 0 || name is null)
        {
            return Invalid("Only a payment that names who it went to can be tracked.");
        }

        var matchText = PaymentKey.Normalize(request.MatchText) ?? name.ToUpperInvariant();
        if (matchText.Length is < PaymentKey.MinLength or > PaymentKey.MaxLength)
        {
            return Invalid($"The text to match needs at least {PaymentKey.MinLength} characters.");
        }

        // A text that misses the very payment being marked is a typo, not a choice.
        if (!PaymentKey.Matches(matchText, transaction.Counterparty, transaction.Description))
        {
            return Invalid("The text to match must appear in this payment.");
        }

        var tracked = await db.RecurringOf(user.HouseholdId).ToListAsync(cancellationToken);
        var existing = tracked.FirstOrDefault(r => r.MatchKey == matchText)
            ?? new RecurringMatcher(tracked).Find(transaction.Counterparty, transaction.Description);
        if (existing is not null)
        {
            existing.Kind = kind;
            existing.MatchKey = matchText;
        }
        else
        {
            db.RecurringPayments.Add(new RecurringPayment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Kind = kind,
                Name = name,
                MatchKey = matchText,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult Invalid(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}
