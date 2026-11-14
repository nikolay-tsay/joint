using CommunityToolkit.Mvvm.ComponentModel;

namespace Joint.Mobile.Features.Categories;

/// <summary>One category in the picker's list.</summary>
/// <param name="isCurrent">True for the category the payment already has.</param>
/// <param name="payees">How many payees are in it; a category with none has nothing to remove.</param>
public partial class CategoryChoice(string name, bool isCurrent, int payees) : ObservableObject
{
    public string Name { get; } = name;

    public bool IsCurrent { get; } = isCurrent;

    public int Payees { get; } = payees;

    public bool CanBeRemoved => Payees > 0;

    /// <summary>What the row says while editing: whether there is anything to remove.</summary>
    public string Usage => Payees switch
    {
        0 => "No payees",
        1 => "1 payee",
        var count => $"{count} payees",
    };

    /// <summary>The question asked in the row before anything is removed.</summary>
    public string RemoveQuestion => Payees == 1
        ? "Take this category off its payee? Its payments go back to having no category."
        : $"Take this category off its {Payees} payees? Their payments go back to having no category.";

    /// <summary>True while the row asks "really remove?" in place.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingRemove { get; set; }
}
