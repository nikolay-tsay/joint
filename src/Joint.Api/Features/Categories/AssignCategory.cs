using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Recurring;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Categories;

/// <summary>
/// Puts the payee of a transaction in a category. From then on every payment containing
/// the match text has it, earlier ones included. The text is suggested from the transaction
/// and may be corrected by the caller. Doing it for a payment that already has a category
/// changes that rule. A name that is not a category yet becomes one.
/// </summary>
public class AssignCategory : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/categories", HandleAsync)
            .WithName("AssignCategory")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound, ProblemHttpResult>> HandleAsync(
        AssignCategoryRequest request,
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

        var rules = await db.CategoryRulesOf(user.HouseholdId).ToListAsync(cancellationToken);
        var category = CategoryNames.Canonical(request.Category, rules.Select(r => r.Category));
        if (category is null)
        {
            return Invalid($"A category needs a name of at most {CategoryNames.MaxLength} characters.");
        }

        var suggestion = PaymentKey.From(transaction.Counterparty, transaction.Description);
        if (transaction.Amount >= 0 || suggestion is null)
        {
            return Invalid("Only a payment that names who it went to can be given a category.");
        }

        var matchText = PaymentKey.Normalize(request.MatchText) ?? suggestion;
        if (matchText.Length is < PaymentKey.MinLength or > PaymentKey.MaxLength)
        {
            return Invalid($"The text to match needs at least {PaymentKey.MinLength} characters.");
        }

        // A text that misses the very payment being categorized is a typo, not a choice.
        if (!PaymentKey.Matches(matchText, transaction.Counterparty, transaction.Description))
        {
            return Invalid("The text to match must appear in this payment.");
        }

        var existing = rules.FirstOrDefault(r => r.MatchKey == matchText)
            ?? new CategoryMatcher(rules).Find(transaction.Counterparty, transaction.Description);
        if (existing is not null)
        {
            existing.Category = category;
            existing.MatchKey = matchText;
        }
        else
        {
            db.CategoryRules.Add(new CategoryRule
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Category = category,
                MatchKey = matchText,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult Invalid(string detail) =>
        TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}
