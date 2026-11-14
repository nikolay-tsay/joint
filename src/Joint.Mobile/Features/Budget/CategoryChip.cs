using CommunityToolkit.Mvvm.ComponentModel;

namespace Joint.Mobile.Features.Budget;

/// <summary>A category that can still be given part of the budget, as a chip; one is picked at a time.</summary>
public partial class CategoryChip(string name) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
