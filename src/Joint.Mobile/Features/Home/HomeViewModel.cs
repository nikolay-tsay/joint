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

    /// <summary>True when the household is shared, so leaving it makes sense.</summary>
    [ObservableProperty]
    public partial bool IsShared { get; set; }

    /// <summary>True when no account has been synced yet, to show a hint instead of an empty list.</summary>
    [ObservableProperty]
    public partial bool HasNoAccounts { get; set; }

    public ObservableCollection<string> Members { get; } = [];

    public ObservableCollection<AccountItem> Accounts { get; } = [];

    /// <summary>One line per connection, saying who owns it and how its last sync went.</summary>
    public ObservableCollection<string> Connections { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var me = await api.GetMeAsync();
        Greeting = $"Hello, {me.DisplayName}";
        HouseholdName = me.Household.Name;
        Members.Clear();
        foreach (var member in me.Household.Members)
        {
            Members.Add(member.DisplayName);
        }

        IsShared = Members.Count > 1;

        Accounts.Clear();
        foreach (var account in await api.GetAccountsAsync())
        {
            Accounts.Add(AccountItem.From(account));
        }

        HasNoAccounts = Accounts.Count == 0;

        Connections.Clear();
        foreach (var connection in await api.GetConnectionsAsync())
        {
            var state = connection.Status switch
            {
                "Active" when connection.LastSyncedAt is { } synced => $"synced {synced.ToLocalTime():g}",
                "Pending" => "first sync in progress",
                "Failing" => connection.LastError ?? "last sync failed",
                var other => other,
            };
            Connections.Add($"{connection.Provider} ({connection.OwnerDisplayName}): {state}");
        }
    });

    [RelayCommand]
    private async Task ConnectLunchFlowAsync()
    {
        var apiKey = await Shell.Current.DisplayPromptAsync(
            "Connect Lunch Flow",
            "Paste the API key from your Lunch Flow API destination. It is stored encrypted "
            + "and only used to read your accounts.",
            "Connect",
            "Cancel");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var problem = await api.ConnectLunchFlowAsync(apiKey);
            await LoadAsync();
            Error = problem;
        });
    }

    [RelayCommand]
    private Task InviteAsync() => RunAsync(async () =>
    {
        var invitation = await api.CreateInvitationAsync();
        var expires = invitation.ExpiresAt.ToLocalTime().ToString("g");
        var share = await Shell.Current.DisplayAlertAsync(
            "Invitation code",
            $"{invitation.Code}\n\nGive this code to the person you want to share your finances with. "
            + $"It works once and expires {expires}.",
            "Share",
            "Close");
        if (share)
        {
            await Share.Default.RequestAsync(new ShareTextRequest(
                $"Join my household in Joint with this code: {invitation.Code}", "Joint invitation"));
        }
    });

    [RelayCommand]
    private async Task JoinAsync()
    {
        var code = await Shell.Current.DisplayPromptAsync(
            "Join a household",
            "Enter the invitation code you were given. You will leave your current household "
            + "and see everything in theirs; they will see your accounts too.",
            "Join",
            "Cancel",
            placeholder: "ABCD-EFGH");
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (!await api.AcceptInvitationAsync(code))
            {
                Error = "That code is invalid, already used or expired.";
                return;
            }

            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Leave household",
            "You will go back to a household of your own. The others will no longer see your "
            + "accounts, and you will no longer see theirs.",
            "Leave",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await api.LeaveHouseholdAsync();
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        await Shell.Current.GoToAsync(Routes.Login);
    }

    /// <summary>Runs a server action with the busy indicator and the shared error handling.</summary>
    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        Error = null;
        try
        {
            await action();
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
}
