using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Transactions;

/// <summary>
/// Takes a payment out of the budget, or puts it back. This is the manual correction for
/// when the automatic rules count something as everyday spending that is not, such as a
/// transfer to an account the app cannot see. Any household member may do it.
/// </summary>
public class UpdateTransaction : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/api/transactions/{transactionId:guid}", HandleAsync)
            .WithName("UpdateTransaction")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound, ProblemHttpResult>> HandleAsync(
        Guid transactionId,
        UpdateTransactionRequest request,
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
            where candidate.Id == transactionId
            select candidate)
            .FirstOrDefaultAsync(cancellationToken);
        if (transaction is null)
        {
            return TypedResults.NotFound();
        }

        // The budget only counts money going out, so there is nothing to exclude on income.
        if (transaction.Amount >= 0)
        {
            return TypedResults.Problem(
                "Only a payment can be excluded from the budget.", statusCode: StatusCodes.Status400BadRequest);
        }

        transaction.ExcludedFromBudget = request.ExcludedFromBudget;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
