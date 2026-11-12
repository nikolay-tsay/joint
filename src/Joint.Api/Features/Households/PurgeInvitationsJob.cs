using Joint.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Households;

/// <summary>
/// Deletes invitations that can no longer be used: expired ones and ones already
/// accepted. Neither is shown anywhere, so keeping them only costs storage.
/// </summary>
public class PurgeInvitationsJob(JointDbContext db, ILogger<PurgeInvitationsJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var dead = await db.HouseholdInvitations
            .Where(i => i.AcceptedAt != null || i.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        db.HouseholdInvitations.RemoveRange(dead);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Purged {Count} dead invitations.", dead.Count);
    }
}
