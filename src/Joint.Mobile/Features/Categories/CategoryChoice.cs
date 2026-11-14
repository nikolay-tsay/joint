using CommunityToolkit.Mvvm.ComponentModel;

namespace Joint.Mobile.Features.Categories;

/// <summary>One category in the picker's list.</summary>
/// <param name="isCurrent">True for the category the payment already has.</param>
/// <param name="payees">How many payees are in it.</param>
public partial class CategoryChoice(string name, bool isCurrent, int payees) : ObservableObject
{
    public string Name { get; } = name;

    public bool IsCurrent { get; } = isCurrent;

    public int Payees { get; } = payees;

    /// <summary>What the row says while editing: how many payees deleting it would affect.</summary>
    public string Usage => Payees switch
    {
        0 => "No payees",
        1 => "1 payee",
        var count => $"{count} payees",
    };

    /// <summary>The question asked in the row before anything is removed.</summary>
    public string RemoveQuestion => Payees switch
    {
        0 => "Delete this category? It will no longer be offered.",
        1 => "Delete this category? Its payee's payments go back to having no category.",
        var count => $"Delete this category? The payments of its {count} payees go back to having no category.",
    };

    /// <summary>True while the row asks "really remove?" in place.</summary>
    [ObservableProperty]
    public partial bool IsConfirmingRemove { get; set; }
}
