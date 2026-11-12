namespace Joint.Mobile.Features.Transactions;

/// <summary>One option in a filter row or a dropdown.</summary>
/// <param name="Kind">The account type it stands for; null means "all".</param>
public record ChoiceItem(string Label, string? Kind, bool IsSelected);
