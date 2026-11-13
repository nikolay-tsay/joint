namespace Joint.Mobile.Common;

/// <summary>The kinds of tracked payment the backend knows, with the names the app shows for them.</summary>
public static class RecurringKinds
{
    public const string Subscription = "Subscription";
    public const string Bill = "Bill";

    public static string Label(string kind) => kind == Bill ? "Regular payment" : "Subscription";
}
