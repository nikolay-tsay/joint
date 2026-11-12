using Joint.Api.Domain;

namespace Joint.Api.Data;

/// <summary>
/// The one place that decides what a household can see: everything owned by its members.
/// Slices start from these instead of filtering by user themselves.
/// </summary>
public static class HouseholdQueries
{
    public static IQueryable<BankConnection> ConnectionsOf(this JointDbContext db, Guid householdId) =>
        from connection in db.BankConnections
        join owner in db.Users on connection.OwnerUserId equals owner.Id
        where owner.HouseholdId == householdId
        select connection;

    public static IQueryable<Account> AccountsOf(this JointDbContext db, Guid householdId) =>
        from account in db.Accounts
        join connection in db.ConnectionsOf(householdId) on account.ConnectionId equals connection.Id
        select account;
}
