using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasIndex(a => new { a.ConnectionId, a.ExternalId }).IsUnique();
        builder.Property(a => a.Kind).HasConversion<string>();
        builder.Property(a => a.Visibility).HasConversion<string>();
        builder.Property(a => a.Balance).HasPrecision(18, 4);
    }
}
