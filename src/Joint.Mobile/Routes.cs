namespace Joint.Mobile;

/// <summary>Shell routes, as declared in AppShell.</summary>
public static class Routes
{
    public const string Login = "//login";
    public const string Home = "//main/home";

    /// <summary>
    /// The Transactions tab. Takes <c>category</c> (a category's name) or
    /// <c>uncategorized=true</c> to open it filtered, and <c>from</c> and <c>to</c> (yyyy-MM-dd) for
    /// the days; without them a category link lists all dates.
    /// </summary>
    public const string Transactions = "//main/transactions";

    /// <summary>One account's transactions; takes <c>accountId</c> and <c>title</c>.</summary>
    public const string AccountTransactions = "account-transactions";

    /// <summary>The guide to how the app's figures are worked out.</summary>
    public const string Guide = "guide";

    /// <summary>
    /// Choosing a payment's category; takes <c>transactionId</c>, <c>payee</c>,
    /// <c>category</c>, <c>ruleId</c> and <c>match</c>.
    /// </summary>
    public const string CategoryPicker = "category";

    /// <summary>The app lock's settings.</summary>
    public const string Security = "security";
}
