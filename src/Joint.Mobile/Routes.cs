namespace Joint.Mobile;

/// <summary>Shell routes, as declared in AppShell.</summary>
public static class Routes
{
    public const string Login = "//login";
    public const string Home = "//main/home";

    /// <summary>One account's transactions; takes <c>accountId</c> and <c>title</c>.</summary>
    public const string AccountTransactions = "account-transactions";

    /// <summary>The guide to how the app's figures are worked out.</summary>
    public const string Guide = "guide";

    /// <summary>The app lock's settings.</summary>
    public const string Security = "security";
}
