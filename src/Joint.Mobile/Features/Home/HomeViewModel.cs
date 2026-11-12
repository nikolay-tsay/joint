using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Api;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Home;

public partial class HomeViewModel(ApiClient api, AuthService auth) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial string? Greeting { get; set; }

    [ObservableProperty]
    public partial string? HouseholdName { get; set; }

    public ObservableCollection<string> Members { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var me = await api.GetMeAsync();
            Greeting = $"Hello, {me.DisplayName}";
            HouseholdName = me.Household.Name;
            Members.Clear();
            foreach (var member in me.Household.Members)
            {
                Members.Add(member.DisplayName);
            }
        }
        catch (SignedOutException)
        {
            await SignOutAsync();
        }
        catch (HttpRequestException)
        {
            Error = "Couldn't reach the server. Pull down to try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        await Shell.Current.GoToAsync(Routes.Login);
    }
}
