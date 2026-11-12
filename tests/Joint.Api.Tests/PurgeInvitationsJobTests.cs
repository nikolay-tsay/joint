using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Api.Features.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Joint.Api.Tests;

public class PurgeInvitationsJobTests
{
    [Fact]
    public async Task Removes_expired_and_accepted_invitations_and_keeps_open_ones()
    {
        await using var db = new JointDbContext(new DbContextOptionsBuilder<JointDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var now = DateTimeOffset.UtcNow;
        var open = Invitation("open", expiresAt: now.AddHours(1));
        db.HouseholdInvitations.AddRange(
            open,
            Invitation("expired", expiresAt: now.AddMinutes(-1)),
            Invitation("accepted", expiresAt: now.AddHours(1), acceptedAt: now.AddMinutes(-5)));
        await db.SaveChangesAsync();

        await new PurgeInvitationsJob(db, NullLogger<PurgeInvitationsJob>.Instance).RunAsync(CancellationToken.None);

        var remaining = await db.HouseholdInvitations.SingleAsync();
        Assert.Equal(open.Id, remaining.Id);
    }

    private static HouseholdInvitation Invitation(
        string codeHash, DateTimeOffset expiresAt, DateTimeOffset? acceptedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            InvitedByUserId = Guid.NewGuid(),
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            AcceptedAt = acceptedAt,
        };
}
