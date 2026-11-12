namespace Joint.Mobile.Features.Profile;

/// <summary>A source of account data the user can connect, as listed under "Connect".</summary>
public record ProviderOption(string Key, string Name, string Description)
{
    public const string LunchFlowKey = "LunchFlow";

    /// <summary>Every connector the app offers. Add new ones here and give them a panel.</summary>
    public static readonly IReadOnlyList<ProviderOption> All =
    [
        new(LunchFlowKey, "Lunch Flow", "Air Bank and many other banks, using a Lunch Flow API key"),
    ];
}
