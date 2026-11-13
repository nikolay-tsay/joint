using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.HasMany(h => h.Members)
            .WithOne()
            .HasForeignKey(u => u.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(h => h.MonthlyBudget).HasPrecision(18, 4);
    }
}
