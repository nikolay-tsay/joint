namespace Joint.Mobile.Features.Profile;

/// <summary>A source of account data the user can connect, as listed under "Connect".</summary>
public record ProviderOption(string Key, string Name, string Description)
{
    public const string LunchFlowKey = "LunchFlow";
    public const string Trading212Key = "Trading212";

    /// <summary>Every connector the app offers. Add new ones here and give them a panel.</summary>
    public static readonly IReadOnlyList<ProviderOption> All =
    [
        new(LunchFlowKey, "Lunch Flow", "Air Bank and many other banks, using a Lunch Flow API key"),
        new(Trading212Key, "Trading 212", "Your investments, using a read-only Trading 212 API key"),
    ];

    /// <summary>The name to show for a provider as the backend spells it.</summary>
    public static string DisplayName(string provider) =>
        All.FirstOrDefault(option => option.Key == provider)?.Name ?? provider;
}
