using Joint.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Households;

public static class HouseholdCleanup
{
    /// <summary>
    /// Tidies the household a user just left: an empty household is removed with all its
    /// invitations; otherwise only the open invitations that user issued are withdrawn.
    /// Stages the changes; the caller saves them together with the move.
    /// </summary>
    public static async Task AfterMemberLeftAsync(
        JointDbContext db, Guid householdId, Guid leftUserId, CancellationToken cancellationToken)
    {
        var othersRemain = await db.Users
            .AnyAsync(u => u.HouseholdId == householdId && u.Id != leftUserId, cancellationToken);

        var invitations = await db.HouseholdInvitations
            .Where(i => i.HouseholdId == householdId)
            .Where(i => !othersRemain || (i.InvitedByUserId == leftUserId && i.AcceptedAt == null))
            .ToListAsync(cancellationToken);
        db.HouseholdInvitations.RemoveRange(invitations);

        if (!othersRemain)
        {
            var household = await db.Households.FirstAsync(h => h.Id == householdId, cancellationToken);
            db.Households.Remove(household);
        }
    }
}
