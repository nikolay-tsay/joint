using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
    {
        builder.HasOne<Household>().WithMany().HasForeignKey(i => i.HouseholdId);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedByUserId);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedByUserId);
        builder.HasIndex(i => i.CodeHash).IsUnique();
    }
}
