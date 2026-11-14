using CommunityToolkit.Mvvm.ComponentModel;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Budget;

/// <summary>
/// One category's part of the budget, or "Everything else": what the budget leaves to the
/// categories without a part of their own. A category's row opens in place to change or
/// remove its amount; "Everything else" only follows from the others.
/// </summary>
public partial class CategoryLineItem : ObservableObject
{
    public const string RestName = "Everything else";

    public CategoryLineItem(string name, decimal limit, decimal spent, string currency, bool isRest = false)
    {
        Name = name;
        Limit = limit;
        IsRest = isRest;

        var left = limit - spent;
        IsOver = left < 0;
        SpentOf = $"{Money.Format(spent, currency)} of {Money.Format(limit, currency)}";
        Left = IsOver ? $"{Money.Format(-left, currency)} over" : $"{Money.Format(left, currency)} left";
        Progress = limit > 0 ? (double)Math.Clamp(spent / limit, 0, 1) : spent > 0 ? 1 : 0;
        AmountInput = Money.ForInput(limit);
    }

    public string Name { get; }

    public decimal Limit { get; }

    /// <summary>"Everything else"; it cannot be opened.</summary>
    public bool IsRest { get; }

    public bool IsOver { get; }

    /// <summary>"750,00 of 6 000,00 CZK".</summary>
    public string SpentOf { get; }

    /// <summary>"5 250,00 CZK left", or "250,00 CZK over".</summary>
    public string Left { get; }

    /// <summary>Spent against the amount, 0 to 1, for the bar.</summary>
    public double Progress { get; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string? AmountInput { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }
}
