using Joint.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Data;

public class JointDbContext(DbContextOptions<JointDbContext> options) : DbContext(options)
{
    public const string Schema = "joint";

    public DbSet<Household> Households => Set<Household>();
    public DbSet<User> Users => Set<User>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Holding> Holdings => Set<Holding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Own schema, so the tables stay apart from Supabase's and Hangfire's.
        modelBuilder.HasDefaultSchema(Schema);

        // Per-entity mapping lives in Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JointDbContext).Assembly);
    }
}
