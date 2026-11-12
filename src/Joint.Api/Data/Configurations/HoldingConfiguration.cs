using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.HasOne<Account>().WithMany().HasForeignKey(h => h.AccountId);
        builder.HasIndex(h => new { h.AccountId, h.Ticker }).IsUnique();
        builder.Property(h => h.Quantity).HasPrecision(28, 10);
        builder.Property(h => h.AveragePrice).HasPrecision(18, 6);
        builder.Property(h => h.CurrentPrice).HasPrecision(18, 6);
    }
}
