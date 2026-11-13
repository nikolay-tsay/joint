using Joint.Api.Domain;
using Joint.Api.Features.Recurring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class RecurringPaymentConfiguration : IEntityTypeConfiguration<RecurringPayment>
{
    public void Configure(EntityTypeBuilder<RecurringPayment> builder)
    {
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId);
        builder.HasIndex(r => new { r.UserId, r.MatchKey }).IsUnique();
        builder.Property(r => r.Kind).HasConversion<string>();
        builder.Property(r => r.Name).HasMaxLength(PaymentKey.MaxLength);
        builder.Property(r => r.MatchKey).HasMaxLength(PaymentKey.MaxLength);
    }
}
