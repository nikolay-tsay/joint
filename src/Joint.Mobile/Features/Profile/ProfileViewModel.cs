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
public partial class ProfileViewModel(ApiClient api, AuthService auth, AppLock appLock, IBiometrics biometrics)
    : PageViewModel(auth)
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

    // ----- App lock card -----

    /// <summary>Set while the card is being filled in, so that does not count as the user flipping the switch.</summary>
    private bool _isSettingLockState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnLockOn), nameof(IsLockMenuOpen), nameof(LockStatus), nameof(PinSetupTitle))]
    public partial bool IsLockOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnLockOn), nameof(IsLockMenuOpen))]
    public partial bool IsPinSetupOpen { get; set; }

    /// <summary>True while the card asks for the PIN before turning the lock off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLockMenuOpen))]
    public partial bool IsTurnOffOpen { get; set; }

    public bool CanTurnLockOn => !IsLockOn && !IsPinSetupOpen;

    public bool IsLockMenuOpen => IsLockOn && !IsPinSetupOpen && !IsTurnOffOpen;

    public string LockStatus => IsLockOn
        ? "On. Joint asks for your PIN every time you come back to it, and hides its contents in the app switcher."
        : "Off. Anyone holding this unlocked phone can open Joint.";

    public string PinSetupTitle => IsLockOn ? "Change PIN" : "Choose a PIN";

    /// <summary>Whether this device has a fingerprint or face enrolled.</summary>
    public bool CanUseBiometrics => biometrics.IsAvailable;

    /// <summary>The "unlock with fingerprint or face" switch.</summary>
    [ObservableProperty]
    public partial bool UseBiometrics { get; set; }

    /// <summary>The PIN in use, asked for before it can be changed or the lock turned off.</summary>
    [ObservableProperty]
    public partial string? CurrentPin { get; set; }

    [ObservableProperty]
    public partial string? NewPin { get; set; }

    [ObservableProperty]
    public partial string? RepeatPin { get; set; }

    [ObservableProperty]
    public partial string? LockError { get; set; }

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        _isSettingLockState = true;
        IsLockOn = await appLock.IsEnabledAsync();
        UseBiometrics = IsLockOn && CanUseBiometrics && await appLock.UsesBiometricsAsync();
        _isSettingLockState = false;

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
                ConnectStep = ConnectStep.Closed;
                LunchFlowKey = null;
                await LoadAsync();
            }
        });
    }

    [RelayCommand]
    private async Task ConfirmTrading212Async()
    {
        if (string.IsNullOrWhiteSpace(Trading212Key) || string.IsNullOrWhiteSpace(Trading212Secret))
        {
            ConnectError = "Paste both the API key and the API secret.";
            return;
        }

        await RunAsync(async () =>
        {
            ConnectError = await api.ConnectTrading212Async(Trading212Key, Trading212Secret);
            if (ConnectError is null)
            {
                ConnectStep = ConnectStep.Closed;
                Trading212Key = null;
                Trading212Secret = null;
                await LoadAsync();
            }
        });
    }

    [RelayCommand]
    private async Task ConfirmFioAsync()
    {
        if (string.IsNullOrWhiteSpace(FioToken))
        {
            ConnectError = "Paste the token first.";
            return;
        }

        await RunAsync(async () =>
        {
            ConnectError = await api.ConnectFioAsync(FioToken);
            if (ConnectError is null)
            {
                ConnectStep = ConnectStep.Closed;
                FioToken = null;
                await LoadAsync();
            }
        });
    }

    // ----- App lock -----

    /// <summary>Opens the PIN fields, to turn the lock on or to change the PIN.</summary>
    [RelayCommand]
    private void StartPinSetup()
    {
        CurrentPin = null;
        NewPin = null;
        RepeatPin = null;
        LockError = null;
        IsPinSetupOpen = true;
    }

    [RelayCommand]
    private void CancelPinSetup() => IsPinSetupOpen = false;

    [RelayCommand]
    private async Task ConfirmPinAsync()
    {
        if (NewPin is not { Length: AppLock.PinLength } pin || !pin.All(char.IsAsciiDigit))
        {
            LockError = $"A PIN is {AppLock.PinLength} digits.";
            return;
        }

        if (pin != RepeatPin)
        {
            LockError = "The two PINs don't match.";
            return;
        }

        var isTurningOn = !IsLockOn;
        if (!isTurningOn && !await ConfirmCurrentPinAsync())
        {
            return;
        }

        await appLock.SetPinAsync(pin);
        CurrentPin = null;
        NewPin = null;
        RepeatPin = null;
        IsPinSetupOpen = false;
        IsLockOn = true;

        // Biometrics are the quicker way in, so offer them straight away; the PIN stays as the fallback.
        if (isTurningOn && CanUseBiometrics)
        {
            UseBiometrics = true;
        }
    }

    /// <summary>Asks for the PIN in place before the lock can be turned off.</summary>
    [RelayCommand]
    private void StartTurnOff()
    {
        CurrentPin = null;
        LockError = null;
        IsTurnOffOpen = true;
    }

    [RelayCommand]
    private void CancelTurnOff() => IsTurnOffOpen = false;

    [RelayCommand]
    private async Task ConfirmTurnOffAsync()
    {
        if (!await ConfirmCurrentPinAsync())
        {
            return;
        }

        appLock.Reset();
        CurrentPin = null;
        IsTurnOffOpen = false;
        _isSettingLockState = true;
        UseBiometrics = false;
        _isSettingLockState = false;
        IsLockOn = false;
    }

    /// <summary>
    /// Checks the PIN typed into the card. Wrong ones count toward the same limit as on
    /// the lock page, and reaching it signs the user out.
    /// </summary>
    private async Task<bool> ConfirmCurrentPinAsync()
    {
        if (string.IsNullOrEmpty(CurrentPin))
        {
            LockError = "Enter your current PIN.";
            return false;
        }

        var (matched, attemptsLeft) = await appLock.CheckPinAsync(CurrentPin);
        if (matched)
        {
            LockError = null;
            return true;
        }

        CurrentPin = null;
        if (attemptsLeft == 0)
        {
            IsPinSetupOpen = false;
            IsTurnOffOpen = false;
            await SignOutAsync();
            return false;
        }

        LockError = attemptsLeft == 1
            ? "Wrong PIN. One attempt left before you are signed out."
            : $"Wrong PIN. {attemptsLeft} attempts left.";
        return false;
    }

    partial void OnUseBiometricsChanged(bool value)
    {
        if (!_isSettingLockState)
        {
            _ = ApplyBiometricsAsync(value);
        }
    }

    /// <summary>Saves the switch, after proving the fingerprint or face works when turning it on.</summary>
    private async Task ApplyBiometricsAsync(bool enabled)
    {
        if (enabled && !await biometrics.AuthenticateAsync("Confirm it's you", "Cancel"))
        {
            _isSettingLockState = true;
            UseBiometrics = false;
            _isSettingLockState = false;
            return;
        }

        await appLock.SetBiometricsAsync(enabled);
    }

    // ----- Sync now (Debug builds only) -----

    /// <summary>Asks for an immediate sync, then reloads once it has had a moment to run.</summary>
    [RelayCommand]
    private Task SyncNowAsync(ConnectionItem connection) => RunAsync(async () =>
    {
        await api.SyncConnectionAsync(connection.Id);
        await Task.Delay(TimeSpan.FromSeconds(4));
        await LoadAsync();
    });

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
