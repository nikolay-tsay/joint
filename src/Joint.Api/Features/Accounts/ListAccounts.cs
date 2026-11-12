using System.Security.Claims;
using Joint.Api.Common;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Accounts;

/// <summary>Lists every account in the caller's household with its current balance.</summary>
public class ListAccounts : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/accounts", HandleAsync)
            .WithName("ListAccounts")
            .RequireAuthorization();

    public static async Task<Results<Ok<List<AccountResponse>>, UnauthorizedHttpResult>> HandleAsync(
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

        var rows = await (
            from account in db.AccountsOf(user.HouseholdId)
            join connection in db.BankConnections on account.ConnectionId equals connection.Id
            join owner in db.Users on connection.OwnerUserId equals owner.Id
            orderby owner.DisplayName, account.Institution, account.Name
            select new { account, owner.DisplayName }).ToListAsync(cancellationToken);

        return TypedResults.Ok(rows
            .Select(row => new AccountResponse(
                row.account.Id,
                row.account.Name,
                row.account.Institution,
                row.account.Kind.ToString(),
                row.account.Currency,
                row.account.Balance,
                row.DisplayName))
            .ToList());
    }
}
