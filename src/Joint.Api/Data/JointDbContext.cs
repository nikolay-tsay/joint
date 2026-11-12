using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Data;

public class JointDbContext(DbContextOptions<JointDbContext> options) : DbContext(options)
{
    public const string Schema = "joint";

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Holding> Holdings => Set<Holding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Own schema, so the tables stay apart from Supabase's and Hangfire's.
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Household>()
            .HasMany(h => h.Members)
            .WithOne()
            .HasForeignKey(m => m.HouseholdId);

        modelBuilder.Entity<HouseholdMember>()
            .HasIndex(m => new { m.HouseholdId, m.UserId })
            .IsUnique();

        modelBuilder.Entity<BankConnection>(connection =>
        {
            connection.HasOne<HouseholdMember>().WithMany().HasForeignKey(c => c.MemberId);
            connection.HasMany(c => c.Accounts).WithOne().HasForeignKey(a => a.ConnectionId);
            connection.Property(c => c.Provider).HasConversion<string>();
            connection.Property(c => c.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Account>(account =>
        {
            account.HasIndex(a => new { a.ConnectionId, a.ExternalId }).IsUnique();
            account.Property(a => a.Kind).HasConversion<string>();
            account.Property(a => a.Visibility).HasConversion<string>();
            account.Property(a => a.Balance).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Transaction>(transaction =>
        {
            transaction.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId);
            transaction.HasIndex(t => new { t.AccountId, t.ExternalId }).IsUnique();
            transaction.HasIndex(t => new { t.AccountId, t.BookedOn });
            transaction.Property(t => t.Amount).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Holding>(holding =>
        {
            holding.HasOne<Account>().WithMany().HasForeignKey(h => h.AccountId);
            holding.HasIndex(h => new { h.AccountId, h.Ticker }).IsUnique();
            holding.Property(h => h.Quantity).HasPrecision(28, 10);
            holding.Property(h => h.AveragePrice).HasPrecision(18, 6);
            holding.Property(h => h.CurrentPrice).HasPrecision(18, 6);
        });
    }
}
