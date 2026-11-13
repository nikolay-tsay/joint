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

    /// <summary>The subscriptions and regular payments marked by anyone in the household.</summary>
    public static IQueryable<RecurringPayment> RecurringOf(this JointDbContext db, Guid householdId) =>
        from recurring in db.RecurringPayments
        join marker in db.Users on recurring.UserId equals marker.Id
        where marker.HouseholdId == householdId
        select recurring;

    /// <summary>The saving goals made by anyone in the household.</summary>
    public static IQueryable<Goal> GoalsOf(this JointDbContext db, Guid householdId) =>
        from goal in db.Goals
        join maker in db.Users on goal.UserId equals maker.Id
        where maker.HouseholdId == householdId
        select goal;

    /// <summary>The category rules made by anyone in the household.</summary>
    public static IQueryable<CategoryRule> CategoryRulesOf(this JointDbContext db, Guid householdId) =>
        from rule in db.CategoryRules
        join maker in db.Users on rule.UserId equals maker.Id
        where maker.HouseholdId == householdId
        select rule;
}
