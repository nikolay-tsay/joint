using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joint.Api.Data.Configurations;

public class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> builder)
    {
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.OwnerUserId);
        builder.HasMany(c => c.Accounts).WithOne().HasForeignKey(a => a.ConnectionId);
        builder.Property(c => c.Provider).HasConversion<string>();
        builder.Property(c => c.Status).HasConversion<string>();
    }
}
