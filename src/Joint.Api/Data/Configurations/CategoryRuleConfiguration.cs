using Joint.Api.Domain;
using Joint.Api.Features.Categories;
using Joint.Api.Features.Recurring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class CategoryRuleConfiguration : IEntityTypeConfiguration<CategoryRule>
{
    public void Configure(EntityTypeBuilder<CategoryRule> builder)
    {
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId);
        builder.HasIndex(r => new { r.UserId, r.MatchKey }).IsUnique();
        builder.Property(r => r.Category).HasMaxLength(CategoryNames.MaxLength);
        builder.Property(r => r.MatchKey).HasMaxLength(PaymentKey.MaxLength);
    }
}
