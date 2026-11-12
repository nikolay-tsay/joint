using System.Security.Claims;
using Joint.Api.Data;
using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Common.Auth;

/// <summary>
/// Resolves the app's <see cref="User"/> for the caller. Supabase owns sign-up, so the
/// first authenticated request from a new person creates their user and a household of one.
/// </summary>
public class CurrentUserService(JointDbContext db)
{
    /// <returns>The user, or null when the token carries no usable identity.</returns>
    public async Task<User?> GetOrCreateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!SupabaseIdentity.TryRead(principal, out var identity))
        {
            return null;
        }

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Id == identity.UserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = $"{identity.DisplayName}'s household",
            CreatedAt = now,
        };
        var user = new User
        {
            Id = identity.UserId,
            HouseholdId = household.Id,
            Email = identity.Email,
            DisplayName = identity.DisplayName,
            CreatedAt = now,
        };
        db.Households.Add(household);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException)
        {
            // Two first requests raced and the other one created the user; use theirs.
            db.Entry(user).State = EntityState.Detached;
            db.Entry(household).State = EntityState.Detached;
            var created = await db.Users.FirstOrDefaultAsync(u => u.Id == identity.UserId, cancellationToken);
            if (created is null)
            {
                throw;
            }

            return created;
        }
    }
}
