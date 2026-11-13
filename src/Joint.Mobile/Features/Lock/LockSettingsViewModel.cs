using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Lock;

/// <summary>
/// The app lock's settings, on a page of their own so nothing here is tapped by accident
/// while scrolling Profile: turn the lock on, change the PIN, switch biometrics, turn it
/// off. Each action happens in place: the card swaps its buttons for a small panel.
/// </summary>
public partial class LockSettingsViewModel(AppLock appLock, IBiometrics biometrics, AuthService auth)
    : PageViewModel(auth)
{
    /// <summary>Set while the page is being filled in, so that does not count as the user flipping the switch.</summary>
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

    /// <summary>Reads the lock's state from the device each time the page is shown.</summary>
    public async Task LoadAsync()
    {
        _isSettingLockState = true;
        IsPinSetupOpen = false;
        IsTurnOffOpen = false;
        IsLockOn = await appLock.IsEnabledAsync();
        UseBiometrics = IsLockOn && CanUseBiometrics && await appLock.UsesBiometricsAsync();
        _isSettingLockState = false;
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

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
}
