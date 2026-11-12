using System.Security.Claims;
using Joint.Api.Common.Auth;
using Joint.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Tests;

public class CurrentUserServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task First_request_creates_the_user_alone_in_a_new_household()
    {
        await using var db = NewDb();
        var service = new CurrentUserService(db);

        var user = await service.GetOrCreateAsync(
            Principal(UserId, "ana@example.com", """{"full_name":"Ana Novak"}"""), CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal(UserId, user.Id);
        Assert.Equal("ana@example.com", user.Email);
        Assert.Equal("Ana Novak", user.DisplayName);

        var household = await db.Households.Include(h => h.Members).SingleAsync();
        Assert.Equal(household.Id, user.HouseholdId);
        Assert.Single(household.Members);
    }

    [Fact]
    public async Task Later_requests_return_the_same_user_without_creating_another()
    {
        await using var db = NewDb();
        var service = new CurrentUserService(db);
        var principal = Principal(UserId, "ana@example.com");

        var first = await service.GetOrCreateAsync(principal, CancellationToken.None);
        var second = await service.GetOrCreateAsync(principal, CancellationToken.None);

        Assert.Equal(first!.HouseholdId, second!.HouseholdId);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Households.CountAsync());
    }

    [Fact]
    public async Task Two_people_start_in_separate_households()
    {
        await using var db = NewDb();
        var service = new CurrentUserService(db);

        var ana = await service.GetOrCreateAsync(Principal(UserId, "ana@example.com"), CancellationToken.None);
        var ben = await service.GetOrCreateAsync(Principal(Guid.NewGuid(), "ben@example.com"), CancellationToken.None);

        Assert.NotEqual(ana!.HouseholdId, ben!.HouseholdId);
    }

    [Fact]
    public async Task Token_without_a_user_id_yields_no_user()
    {
        await using var db = NewDb();
        var service = new CurrentUserService(db);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "ana@example.com")], "test"));

        Assert.Null(await service.GetOrCreateAsync(principal, CancellationToken.None));
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Theory]
    [InlineData("""{"full_name":"Ana Novak","name":"ana"}""", "Ana Novak")]
    [InlineData("""{"name":"Ana N."}""", "Ana N.")]
    [InlineData("""{"full_name":"  "}""", "ana")]
    [InlineData("not json", "ana")]
    [InlineData(null, "ana")]
    public void Display_name_prefers_provider_name_then_email(string? metadata, string expected)
    {
        Assert.True(SupabaseIdentity.TryRead(Principal(UserId, "ana@example.com", metadata), out var identity));
        Assert.Equal(expected, identity.DisplayName);
    }

    [Fact]
    public async Task The_profile_picture_is_stored_and_follows_changes_at_the_provider()
    {
        await using var db = NewDb();
        var service = new CurrentUserService(db);

        var created = await service.GetOrCreateAsync(
            Principal(UserId, "ana@example.com", """{"avatar_url":"https://lh3.googleusercontent.com/a/one"}"""),
            CancellationToken.None);
        Assert.Equal("https://lh3.googleusercontent.com/a/one", created!.AvatarUrl);

        await service.GetOrCreateAsync(
            Principal(UserId, "ana@example.com", """{"picture":"https://lh3.googleusercontent.com/a/two"}"""),
            CancellationToken.None);
        Assert.Equal("https://lh3.googleusercontent.com/a/two", (await db.Users.SingleAsync()).AvatarUrl);

        // A token without a picture does not wipe the one already known.
        await service.GetOrCreateAsync(Principal(UserId, "ana@example.com"), CancellationToken.None);
        Assert.Equal("https://lh3.googleusercontent.com/a/two", (await db.Users.SingleAsync()).AvatarUrl);
    }

    [Theory]
    [InlineData("http://example.com/a.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void A_picture_link_that_is_not_plain_https_is_ignored(string avatarUrl)
    {
        var metadata = $$"""{"avatar_url":"{{avatarUrl}}"}""";

        Assert.True(SupabaseIdentity.TryRead(Principal(UserId, "ana@example.com", metadata), out var identity));
        Assert.Null(identity.AvatarUrl);
    }

    private static JointDbContext NewDb() =>
        new(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Principal(Guid userId, string email, string? userMetadata = null)
    {
        List<Claim> claims = [new("sub", userId.ToString()), new("email", email)];
        if (userMetadata is not null)
        {
            claims.Add(new Claim("user_metadata", userMetadata));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
