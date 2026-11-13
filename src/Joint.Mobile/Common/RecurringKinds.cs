namespace Joint.Mobile.Common;

/// <summary>The kinds of marked payee the backend knows, with the names the app shows for them.</summary>
public static class RecurringKinds
{
    public const string Subscription = "Subscription";
    public const string Bill = "Bill";

    /// <summary>Money moved to or from an own account the app cannot pair up; not spending.</summary>
    public const string Transfer = "Transfer";

    public static string Label(string kind) => kind switch
    {
        Bill => "Regular payment",
        Transfer => "Own transfer",
        _ => "Subscription",
    };
}
