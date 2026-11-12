using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Profile;

/// <summary>Who you are, who you share with, and where your data comes from.</summary>
public partial class ProfileViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    [ObservableProperty]
    public partial string? DisplayName { get; set; }

    [ObservableProperty]
    public partial string? Email { get; set; }

    /// <summary>First letter of the name, shown in the avatar.</summary>
    [ObservableProperty]
    public partial string? Initial { get; set; }

    [ObservableProperty]
    public partial string? HouseholdName { get; set; }

    /// <summary>True when the household has more than one member, so leaving makes sense.</summary>
    [ObservableProperty]
    public partial bool IsShared { get; set; }

    [ObservableProperty]
    public partial bool HasNoConnections { get; set; }

    public ObservableCollection<string> Members { get; } = [];

    public ObservableCollection<ConnectionItem> Connections { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var me = await api.GetMeAsync();
        DisplayName = me.DisplayName;
        Email = me.Email;
        Initial = me.DisplayName.Length > 0 ? me.DisplayName[..1].ToUpperInvariant() : "?";
        HouseholdName = me.Household.Name;

        Members.Clear();
        foreach (var member in me.Household.Members)
        {
            Members.Add(member.UserId == me.UserId ? $"{member.DisplayName} (you)" : member.DisplayName);
        }

        IsShared = Members.Count > 1;

        Connections.Clear();
        foreach (var connection in await api.GetConnectionsAsync())
        {
            Connections.Add(ConnectionItem.From(connection));
        }

        HasNoConnections = Connections.Count == 0;
    });

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
            var joined = await api.AcceptInvitationAsync(code);
            await LoadAsync();
            if (!joined)
            {
                Error = "That code is invalid, already used or expired.";
            }
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
    private async Task RemoveConnectionAsync(ConnectionItem connection)
    {
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Remove connection",
            "Its accounts and transactions will be deleted from Joint for everyone in your "
            + "household. Nothing changes at your bank.",
            "Remove",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await api.DeleteConnectionAsync(connection.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private Task SignOutNowAsync() => SignOutAsync();
}
