using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Profile;

/// <summary>
/// Who you are, who you share with, and where your data comes from. Every action happens
/// in place on the page: a card swaps its buttons for a small panel instead of opening a dialog.
/// </summary>
public partial class ProfileViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    [ObservableProperty]
    public partial string? DisplayName { get; set; }

    [ObservableProperty]
    public partial string? Email { get; set; }

    /// <summary>First letter of the name, shown in the avatar.</summary>
    [ObservableProperty]
    public partial string? Initial { get; set; }

    /// <summary>The Google profile picture; the initial shows when there is none.</summary>
    [ObservableProperty]
    public partial string? AvatarUrl { get; set; }

    [ObservableProperty]
    public partial string? HouseholdName { get; set; }

    /// <summary>True when the household has more than one member, so leaving makes sense.</summary>
    [ObservableProperty]
    public partial bool IsShared { get; set; }

    [ObservableProperty]
    public partial bool HasNoConnections { get; set; }

    public ObservableCollection<MemberItem> Members { get; } = [];

    public ObservableCollection<ConnectionItem> Connections { get; } = [];

    // ----- Household card -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHouseholdMenuOpen), nameof(IsInviteOpen), nameof(IsJoinOpen), nameof(IsLeaveOpen))]
    public partial HouseholdPanel Panel { get; set; }

    public bool IsHouseholdMenuOpen => Panel == HouseholdPanel.None;

    public bool IsInviteOpen => Panel == HouseholdPanel.Invite;

    public bool IsJoinOpen => Panel == HouseholdPanel.Join;

    public bool IsLeaveOpen => Panel == HouseholdPanel.Leave;

    [ObservableProperty]
    public partial string? InvitationCode { get; set; }

    /// <summary>What to do with the code and when it stops working.</summary>
    [ObservableProperty]
    public partial string? InvitationHint { get; set; }

    /// <summary>What the user is typing into the join panel.</summary>
    [ObservableProperty]
    public partial string? JoinCode { get; set; }

    /// <summary>Why the last join attempt failed, shown under the field.</summary>
    [ObservableProperty]
    public partial string? JoinError { get; set; }

    // ----- Connections card -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectClosed))]
    public partial bool IsConnectOpen { get; set; }

    public bool IsConnectClosed => !IsConnectOpen;

    /// <summary>What the user is pasting into the connect panel.</summary>
    [ObservableProperty]
    public partial string? LunchFlowKey { get; set; }

    [ObservableProperty]
    public partial string? ConnectError { get; set; }

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        var me = await api.GetMeAsync();
        DisplayName = me.DisplayName;
        Email = me.Email;
        Initial = me.DisplayName.Length > 0 ? me.DisplayName[..1].ToUpperInvariant() : "?";
        AvatarUrl = me.AvatarUrl;
        HouseholdName = me.Household.Name;

        Members.Clear();
        foreach (var member in me.Household.Members)
        {
            Members.Add(MemberItem.From(member, isCurrentUser: member.UserId == me.UserId));
        }

        IsShared = Members.Count > 1;

        // Fetch first, then swap: clearing before the await lets two loads interleave and double up.
        var connections = await api.GetConnectionsAsync();
        Connections.Clear();
        foreach (var connection in connections)
        {
            Connections.Add(ConnectionItem.From(connection));
        }

        HasNoConnections = Connections.Count == 0;
    });

    // ----- Invite -----

    /// <summary>Creates a code and shows it in the card.</summary>
    [RelayCommand]
    private Task InviteAsync() => RunAsync(async () =>
    {
        var invitation = await api.CreateInvitationAsync();
        InvitationCode = invitation.Code;
        InvitationHint =
            "Give this code to the person you want to share your finances with. "
            + $"It works once and expires {invitation.ExpiresAt.ToLocalTime():g}.";
        Panel = HouseholdPanel.Invite;
    });

    [RelayCommand]
    private Task ShareInvitationAsync() =>
        Share.Default.RequestAsync(new ShareTextRequest(
            $"Join my household in Joint with this code: {InvitationCode}", "Joint invitation"));

    [RelayCommand]
    private async Task CopyInvitationAsync()
    {
        await Clipboard.Default.SetTextAsync(InvitationCode);
        InvitationHint = "Copied. The code works once.";
    }

    // ----- Join -----

    [RelayCommand]
    private void StartJoin()
    {
        JoinCode = null;
        JoinError = null;
        Panel = HouseholdPanel.Join;
    }

    [RelayCommand]
    private async Task ConfirmJoinAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinCode))
        {
            JoinError = "Enter the code first.";
            return;
        }

        await RunAsync(async () =>
        {
            if (await api.AcceptInvitationAsync(JoinCode))
            {
                Panel = HouseholdPanel.None;
                await LoadAsync();
            }
            else
            {
                JoinError = "That code is invalid, already used or expired.";
            }
        });
    }

    // ----- Leave -----

    [RelayCommand]
    private void StartLeave() => Panel = HouseholdPanel.Leave;

    [RelayCommand]
    private Task ConfirmLeaveAsync() => RunAsync(async () =>
    {
        await api.LeaveHouseholdAsync();
        Panel = HouseholdPanel.None;
        await LoadAsync();
    });

    /// <summary>Back to the household buttons from any panel.</summary>
    [RelayCommand]
    private void CloseHouseholdPanel() => Panel = HouseholdPanel.None;

    // ----- Connect Lunch Flow -----

    [RelayCommand]
    private void StartConnect()
    {
        LunchFlowKey = null;
        ConnectError = null;
        IsConnectOpen = true;
    }

    [RelayCommand]
    private void CancelConnect() => IsConnectOpen = false;

    [RelayCommand]
    private async Task ConfirmConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(LunchFlowKey))
        {
            ConnectError = "Paste the API key first.";
            return;
        }

        await RunAsync(async () =>
        {
            ConnectError = await api.ConnectLunchFlowAsync(LunchFlowKey);
            if (ConnectError is null)
            {
                IsConnectOpen = false;
                LunchFlowKey = null;
                await LoadAsync();
            }
        });
    }

    // ----- Remove a connection -----

    [RelayCommand]
    private void StartRemove(ConnectionItem connection)
    {
        foreach (var other in Connections)
        {
            other.IsConfirmingRemove = false;
        }

        connection.IsConfirmingRemove = true;
    }

    [RelayCommand]
    private static void CancelRemove(ConnectionItem connection) => connection.IsConfirmingRemove = false;

    [RelayCommand]
    private Task ConfirmRemoveAsync(ConnectionItem connection) => RunAsync(async () =>
    {
        await api.DeleteConnectionAsync(connection.Id);
        await LoadAsync();
    });

    [RelayCommand]
    private Task SignOutNowAsync() => SignOutAsync();
}
