using Joint.Api.Domain;
using Joint.Api.Features.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.HasOne<User>().WithMany().HasForeignKey(g => g.UserId);
        builder.Property(g => g.Kind).HasConversion<string>();
        builder.Property(g => g.Name).HasMaxLength(GoalRules.MaxNameLength);
        builder.Property(g => g.TargetAmount).HasPrecision(18, 4);
    }
}
