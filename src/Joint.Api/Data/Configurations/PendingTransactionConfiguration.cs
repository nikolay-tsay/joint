using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class PendingTransactionConfiguration : IEntityTypeConfiguration<PendingTransaction>
{
    public void Configure(EntityTypeBuilder<PendingTransaction> builder)
    {
        // Goes with its account, which goes with its connection.
        builder.HasOne<Account>().WithMany().HasForeignKey(p => p.AccountId);
        builder.HasIndex(p => p.AccountId);
        builder.Property(p => p.Amount).HasPrecision(18, 4);
    }
}
