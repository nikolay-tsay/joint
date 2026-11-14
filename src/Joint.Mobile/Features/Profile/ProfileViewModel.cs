using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;
using Joint.Mobile.Features.Lock;

namespace Joint.Mobile.Features.Profile;

/// <summary>
/// Who you are, who you share with, and where your data comes from. Every action happens
/// in place on the page: a card swaps its buttons for a small panel instead of opening a dialog.
/// </summary>
public partial class ProfileViewModel(ApiClient api, AuthService auth, AppLock appLock) : PageViewModel(auth)
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
    [NotifyPropertyChangedFor(
        nameof(IsConnectClosed), nameof(IsChoosingProvider), nameof(IsLunchFlowOpen), nameof(IsTrading212Open),
        nameof(IsFioOpen))]
    public partial ConnectStep ConnectStep { get; set; }

    public bool IsConnectClosed => ConnectStep == ConnectStep.Closed;

    public bool IsChoosingProvider => ConnectStep == ConnectStep.ChoosingProvider;

    public bool IsLunchFlowOpen => ConnectStep == ConnectStep.LunchFlow;

    public bool IsTrading212Open => ConnectStep == ConnectStep.Trading212;

    public bool IsFioOpen => ConnectStep == ConnectStep.Fio;

    /// <summary>The connectors offered after tapping "Connect".</summary>
    public IReadOnlyList<ProviderOption> Providers { get; } = ProviderOption.All;

    /// <summary>What the user is pasting into the Lunch Flow panel.</summary>
    [ObservableProperty]
    public partial string? LunchFlowKey { get; set; }

    /// <summary>What the user is pasting into the Trading 212 panel.</summary>
    [ObservableProperty]
    public partial string? Trading212Key { get; set; }

    [ObservableProperty]
    public partial string? Trading212Secret { get; set; }

    /// <summary>What the user is pasting into the Fio panel.</summary>
    [ObservableProperty]
    public partial string? FioToken { get; set; }

    [ObservableProperty]
    public partial string? ConnectError { get; set; }

    /// <summary>
    /// True from tapping "Connect" until the backend answers. The provider checks the
    /// credential in that time, which takes seconds and with Fio can take a minute, so
    /// the panel swaps its buttons for a spinner and <see cref="ConnectProgress"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotConnecting))]
    public partial bool IsConnecting { get; set; }

    public bool IsNotConnecting => !IsConnecting;

    /// <summary>What is being waited for while connecting.</summary>
    [ObservableProperty]
    public partial string? ConnectProgress { get; set; }

    /// <summary>Whether the app lock is on, in a word; its settings are on a page of their own.</summary>
    [ObservableProperty]
    public partial string? LockSummary { get; set; }

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        LockSummary = await appLock.IsEnabledAsync()
            ? "On. Joint asks for your PIN every time you come back to it."
            : "Off. Anyone holding this unlocked phone can open Joint.";

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

    // ----- Connect -----

    /// <summary>Shows the list of connectors to pick from.</summary>
    [RelayCommand]
    private void StartConnect() => ConnectStep = ConnectStep.ChoosingProvider;

    /// <summary>Opens the chosen connector's own form.</summary>
    [RelayCommand]
    private void ChooseProvider(ProviderOption provider)
    {
        LunchFlowKey = null;
        Trading212Key = null;
        Trading212Secret = null;
        FioToken = null;
        ConnectError = null;
        ConnectStep = provider.Key switch
        {
            ProviderOption.LunchFlowKey => ConnectStep.LunchFlow,
            ProviderOption.Trading212Key => ConnectStep.Trading212,
            ProviderOption.FioKey => ConnectStep.Fio,
            _ => ConnectStep.Closed,
        };
    }

    [RelayCommand]
    private void CancelConnect() => ConnectStep = ConnectStep.Closed;

    [RelayCommand]
    private Task ConfirmConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(LunchFlowKey))
        {
            ConnectError = "Paste the API key first.";
            return Task.CompletedTask;
        }

        return ConnectAsync(
            "Checking the key with Lunch Flow…",
            () => api.ConnectLunchFlowAsync(LunchFlowKey),
            () => LunchFlowKey = null);
    }

    [RelayCommand]
    private Task ConfirmTrading212Async()
    {
        if (string.IsNullOrWhiteSpace(Trading212Key) || string.IsNullOrWhiteSpace(Trading212Secret))
        {
            ConnectError = "Paste both the API key and the API secret.";
            return Task.CompletedTask;
        }

        return ConnectAsync(
            "Checking the key with Trading 212. This can take a few seconds…",
            () => api.ConnectTrading212Async(Trading212Key, Trading212Secret),
            () =>
            {
                Trading212Key = null;
                Trading212Secret = null;
            });
    }

    [RelayCommand]
    private Task ConfirmFioAsync()
    {
        if (string.IsNullOrWhiteSpace(FioToken))
        {
            ConnectError = "Paste the token first.";
            return Task.CompletedTask;
        }

        // Fio lets a token be used once every 30 seconds, so a second try has to wait that out.
        return ConnectAsync(
            "Checking the token with Fio. This can take up to a minute…",
            () => api.ConnectFioAsync(FioToken),
            () => FioToken = null);
    }

    /// <summary>
    /// Sends a credential to the backend and shows that it is being waited for. A second
    /// tap while one is on its way is ignored.
    /// </summary>
    /// <param name="progress">What the panel says while waiting.</param>
    /// <param name="connect">Returns null on success, otherwise a message to show.</param>
    /// <param name="clear">Forgets what was typed, once it is connected.</param>
    private async Task ConnectAsync(string progress, Func<Task<string?>> connect, Action clear)
    {
        if (IsConnecting)
        {
            return;
        }

        ConnectError = null;
        ConnectProgress = progress;
        IsConnecting = true;
        try
        {
            await RunAsync(async () =>
            {
                ConnectError = await connect();
                if (ConnectError is null)
                {
                    ConnectStep = ConnectStep.Closed;
                    clear();
                    await LoadAsync();
                }
            });

            // A network failure is reported on the page's own error line, far above this
            // panel; say it here too, where the user is looking.
            ConnectError ??= Error;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <summary>Opens the app lock's settings, kept off this page so they are not tapped by accident.</summary>
    [RelayCommand]
    private static Task OpenSecurityAsync() => Shell.Current.GoToAsync(Routes.Security);

    // ----- Sync now (Debug builds only) -----

    /// <summary>Asks for an immediate sync, then reloads once it has had a moment to run.</summary>
    [RelayCommand]
    private Task SyncNowAsync(ConnectionItem connection) => RunAsync(async () =>
    {
        await api.SyncConnectionAsync(connection.Id);
        await Task.Delay(TimeSpan.FromSeconds(4));
        await LoadAsync();
    });

    // ----- Open and remove a connection -----

    /// <summary>Opens a connection's row to show what can be done with it, closing any other.</summary>
    [RelayCommand]
    private void ToggleConnection(ConnectionItem connection)
    {
        var open = !connection.IsOpen;
        foreach (var other in Connections)
        {
            other.IsOpen = false;
            other.IsConfirmingRemove = false;
        }

        connection.IsOpen = open;
    }

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

    /// <summary>Opens the guide to how the app's figures are worked out.</summary>
    [RelayCommand]
    private static Task OpenGuideAsync() => Shell.Current.GoToAsync(Routes.Guide);

    [RelayCommand]
    private Task SignOutNowAsync() => SignOutAsync();
}
