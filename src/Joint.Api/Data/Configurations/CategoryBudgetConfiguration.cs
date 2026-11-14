using Joint.Api.Domain;
using Joint.Api.Features.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class CategoryBudgetConfiguration : IEntityTypeConfiguration<CategoryBudget>
{
    public void Configure(EntityTypeBuilder<CategoryBudget> builder)
    {
        // Goes with its household.
        builder.HasOne<Household>().WithMany().HasForeignKey(b => b.HouseholdId);
        builder.HasIndex(b => new { b.HouseholdId, b.Category }).IsUnique();
        builder.Property(b => b.Category).HasMaxLength(CategoryNames.MaxLength);
        builder.Property(b => b.Amount).HasPrecision(18, 4);
    }
}
