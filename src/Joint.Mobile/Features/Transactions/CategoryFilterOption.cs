using CommunityToolkit.Mvvm.ComponentModel;

namespace Joint.Mobile.Features.Transactions;

/// <summary>One category in the Transactions filter, ticked on and off before the filter is applied.</summary>
/// <param name="name">The category's name; null stands for payments with no category.</param>
public partial class CategoryFilterOption(string? name, bool isSelected) : ObservableObject
{
    public const string NoCategoryLabel = "No category";

    public string? Name { get; } = name;

    public string Label => Name ?? NoCategoryLabel;

    public bool IsNoCategory => Name is null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
