namespace Joint.Mobile.Common;

/// <summary>The account types the backend knows, with the names the app shows for them.</summary>
public static class AccountKinds
{
    public const string Current = "Current";
    public const string Savings = "Savings";
    public const string Investment = "Investment";

    /// <summary>In the order they appear on Home.</summary>
    public static readonly IReadOnlyList<string> All = [Current, Savings, Investment];

    public static string Label(string kind) => kind switch
    {
        Savings => "Savings",
        Investment => "Investments",
        _ => "Everyday",
    };

    /// <summary>The backend value for a label picked in the app; null if it is not one of ours.</summary>
    public static string? FromLabel(string? label) => All.FirstOrDefault(kind => Label(kind) == label);
}
