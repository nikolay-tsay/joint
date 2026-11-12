using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId);
        builder.HasIndex(t => new { t.AccountId, t.ExternalId }).IsUnique();
        builder.HasIndex(t => new { t.AccountId, t.BookedOn });
        builder.Property(t => t.Amount).HasPrecision(18, 4);
    }
}
