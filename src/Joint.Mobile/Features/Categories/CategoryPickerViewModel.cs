using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Categories;

/// <summary>
/// Choosing a category for a payment, on a page of its own because the list can be long.
/// The choice applies to every payment containing the match text, which is suggested and
/// can be edited here. Typing a new name makes a new category.
/// </summary>
public partial class CategoryPickerViewModel(ApiClient api, AuthService auth) : PageViewModel(auth), IQueryAttributable
{
    private Guid _transactionId;
    private Guid? _ruleId;
    private string? _current;

    /// <summary>Who the payment went to, as the Transactions list shows it.</summary>
    [ObservableProperty]
    public partial string? Payee { get; set; }

    /// <summary>Every payment containing this gets the chosen category.</summary>
    [ObservableProperty]
    public partial string? MatchText { get; set; }

    /// <summary>A name for a category that is not in the list yet.</summary>
    [ObservableProperty]
    public partial string? NewName { get; set; }

    /// <summary>True when the payment has a category, which can then be taken off.</summary>
    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    public ObservableCollection<CategoryChoice> Choices { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _transactionId = Guid.TryParse(Text(query, "transactionId"), out var id) ? id : Guid.Empty;
        _ruleId = Guid.TryParse(Text(query, "ruleId"), out var ruleId) ? ruleId : null;
        _current = Text(query, "category");
        Payee = Text(query, "payee");
        MatchText = Text(query, "match");
        CanRemove = _ruleId is not null;
    }

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        var categories = await api.GetCategoriesAsync(null);
        Choices.Clear();
        foreach (var name in categories.Names)
        {
            Choices.Add(new CategoryChoice(name, name == _current));
        }
    });

    [RelayCommand]
    private Task ChooseAsync(CategoryChoice choice) => AssignAsync(choice.Name);

    /// <summary>Makes a category of the typed name and gives it to the payment.</summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            Error = "Type a name for the new category first.";
            return;
        }

        await AssignAsync(NewName);
    }

    /// <summary>Takes the category off the payee; its payments go back to uncategorized.</summary>
    [RelayCommand]
    private Task RemoveAsync() => RunAsync(async () =>
    {
        if (_ruleId is { } ruleId)
        {
            await api.RemoveCategoryRuleAsync(ruleId);
        }

        await GoBackAsync();
    });

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    private Task AssignAsync(string category) => RunAsync(async () =>
    {
        if (await api.AssignCategoryAsync(_transactionId, category, MatchText))
        {
            await GoBackAsync();
        }
        else
        {
            Error = "That didn't work. The text to match must be part of this payment and at least 3 characters long, "
                + "and a category name can have up to 50 characters.";
        }
    });

    private static string? Text(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) && value?.ToString() is { Length: > 0 } text
            ? Uri.UnescapeDataString(text)
            : null;
}
