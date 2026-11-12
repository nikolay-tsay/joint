using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Joint.Api.Features.Households;
using Joint.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class HouseholdInvitationTests
{
    private static readonly ClaimsPrincipal Ana = Principal("ana@example.com");
    private static readonly ClaimsPrincipal Ben = Principal("ben@example.com");
    private static readonly ClaimsPrincipal Cyd = Principal("cyd@example.com");

    [Fact]
    public async Task Accepting_a_code_puts_both_people_in_the_inviters_household()
    {
        await using var db = NewDb();
        var code = await InviteAsync(db, Ana);

        var result = await AcceptAsync(db, Ben, code);

        Assert.IsType<NoContent>(result.Result);
        var ana = await UserAsync(db, Ana);
        var ben = await UserAsync(db, Ben);
        Assert.Equal(ana.HouseholdId, ben.HouseholdId);
    }

    [Fact]
    public async Task Joining_removes_the_household_the_person_left_empty()
    {
        await using var db = NewDb();
        var code = await InviteAsync(db, Ana);
        var bensOldHousehold = (await UserAsync(db, Ben)).HouseholdId;

        await AcceptAsync(db, Ben, code);

        Assert.False(await db.Households.AnyAsync(h => h.Id == bensOldHousehold));
        Assert.Equal(1, await db.Households.CountAsync());
    }

    [Fact]
    public async Task The_code_is_accepted_however_it_is_typed()
    {
        await using var db = NewDb();
        var code = await InviteAsync(db, Ana);

        var result = await AcceptAsync(db, Ben, $"  {code.Replace("-", " ").ToLowerInvariant()} ");

        Assert.IsType<NoContent>(result.Result);
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        await using var db = NewDb();
        var code = await InviteAsync(db, Ana);
        await AcceptAsync(db, Ben, code);

        var result = await AcceptAsync(db, Cyd, code);

        AssertProblem(result, StatusCodes.Status404NotFound);
        Assert.NotEqual((await UserAsync(db, Ana)).HouseholdId, (await UserAsync(db, Cyd)).HouseholdId);
    }

    [Fact]
    public async Task An_expired_code_is_rejected()
    {
        await using var db = NewDb();
        var code = await InviteAsync(db, Ana);
        var invitation = await db.HouseholdInvitations.SingleAsync();
        invitation.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        AssertProblem(await AcceptAsync(db, Ben, code), StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task An_unknown_code_is_rejected()
    {
        await using var db = NewDb();
        await InviteAsync(db, Ana);

        AssertProblem(await AcceptAsync(db, Ben, "AAAA-AAAA"), StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Your_own_code_is_rejected()
    {
        await using var db = NewDb();
        var code = await InviteAsync(db, Ana);

        AssertProblem(await AcceptAsync(db, Ana, code), StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Leaving_gives_the_person_a_household_of_their_own_again()
    {
        await using var db = NewDb();
        await AcceptAsync(db, Ben, await InviteAsync(db, Ana));

        var result = await LeaveAsync(db, Ben);

        Assert.IsType<NoContent>(result.Result);
        var ana = await UserAsync(db, Ana);
        var ben = await UserAsync(db, Ben);
        Assert.NotEqual(ana.HouseholdId, ben.HouseholdId);
        Assert.Equal(2, await db.Households.CountAsync());
    }

    [Fact]
    public async Task Leaving_withdraws_the_open_invitations_that_person_issued()
    {
        await using var db = NewDb();
        await AcceptAsync(db, Ben, await InviteAsync(db, Ana));
        var bensCode = await InviteAsync(db, Ben);

        await LeaveAsync(db, Ben);

        AssertProblem(await AcceptAsync(db, Cyd, bensCode), StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Leaving_a_household_of_one_is_refused()
    {
        await using var db = NewDb();
        await UserAsync(db, Ana);

        AssertProblem(await LeaveAsync(db, Ana), StatusCodes.Status409Conflict);
    }

    [Fact]
    public void Codes_are_readable_and_hash_the_same_regardless_of_formatting()
    {
        var code = InvitationCode.Generate();

        Assert.Matches("^[A-HJKMNP-Z2-9]{4}-[A-HJKMNP-Z2-9]{4}$", code);
        Assert.Equal(InvitationCode.Hash(code), InvitationCode.Hash(code.ToLowerInvariant().Replace("-", "")));
        Assert.NotEqual(InvitationCode.Hash(code), InvitationCode.Hash(InvitationCode.Generate()));
    }

    private static async Task<string> InviteAsync(JointDbContext db, ClaimsPrincipal principal)
    {
        var result = await CreateInvitation.HandleAsync(
            principal, new CurrentUserService(db), db, CancellationToken.None);
        return Assert.IsType<Ok<CreateInvitationResponse>>(result.Result).Value!.Code;
    }

    private static Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> AcceptAsync(
        JointDbContext db, ClaimsPrincipal principal, string code) =>
        AcceptInvitation.HandleAsync(
            new AcceptInvitationRequest(code), principal, new CurrentUserService(db), db, CancellationToken.None);

    private static Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> LeaveAsync(
        JointDbContext db, ClaimsPrincipal principal) =>
        LeaveHousehold.HandleAsync(principal, new CurrentUserService(db), db, CancellationToken.None);

    private static async Task<Domain.User> UserAsync(JointDbContext db, ClaimsPrincipal principal) =>
        (await new CurrentUserService(db).GetOrCreateAsync(principal, CancellationToken.None))!;

    private static void AssertProblem(
        Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult> result, int expectedStatus) =>
        Assert.Equal(expectedStatus, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(string email) =>
        new(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", email)], "test"));
}
