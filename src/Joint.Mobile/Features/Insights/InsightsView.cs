namespace Joint.Mobile.Features.Insights;

/// <summary>
/// One of the pages the Insights carousel slides between. Each page is drawn from the same
/// view model; the key only says which layout it gets.
/// </summary>
public record InsightsView(string Key, InsightsViewModel Model)
{
    public const string Overview = "Overview";
    public const string Recurring = "Recurring";
}
