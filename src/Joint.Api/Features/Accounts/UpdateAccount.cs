using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Accounts;

/// <summary>
/// Sets what kind of account this is (everyday, savings or investment), which decides
/// where it is grouped. Any household member may do it, since they all share one view.
/// </summary>
public class UpdateAccount : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/api/accounts/{accountId:guid}", HandleAsync)
            .WithName("UpdateAccount")
            .RequireAuthorization();

    public static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound, ProblemHttpResult>> HandleAsync(
        Guid accountId,
        UpdateAccountRequest request,
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
        if (!Enum.GetNames<AccountKind>().Contains(request.Kind, StringComparer.OrdinalIgnoreCase)
            || !Enum.TryParse<AccountKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return TypedResults.Problem(
                $"Kind must be one of: {string.Join(", ", Enum.GetNames<AccountKind>())}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var account = await db.AccountsOf(user.HouseholdId)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        account.Kind = kind;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
