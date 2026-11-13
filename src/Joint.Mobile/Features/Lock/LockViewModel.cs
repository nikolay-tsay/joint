using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Lock;

/// <summary>The lock page: biometrics first when they are on, the PIN pad always.</summary>
public partial class LockViewModel(AppLock appLock, IBiometrics biometrics, AuthService auth) : ObservableObject
{
    private readonly TaskCompletionSource<bool> _done = new();
    private string _entered = "";
    private bool _isChecking;

    /// <summary>Completes with true once unlocked, or false once signed out.</summary>
    public Task<bool> Done => _done.Task;

    /// <summary>One mark per PIN digit, filled as far as the user has typed.</summary>
    [ObservableProperty]
    public partial string Dots { get; set; } = ToDots(0);

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Whether to offer the biometric button.</summary>
    [ObservableProperty]
    public partial bool CanUseBiometrics { get; set; }

    /// <summary>Called when the page shows: goes straight to the biometric prompt when it is on.</summary>
    public async Task StartAsync()
    {
        CanUseBiometrics = biometrics.IsAvailable && await appLock.UsesBiometricsAsync();
        if (CanUseBiometrics)
        {
            await UseBiometricsAsync();
        }
    }

    [RelayCommand]
    private async Task UseBiometricsAsync()
    {
        if (await biometrics.AuthenticateAsync("Unlock Joint", "Use PIN"))
        {
            _done.TrySetResult(true);
        }
    }

    [RelayCommand]
    private async Task DigitAsync(string digit)
    {
        if (_isChecking || _entered.Length >= AppLock.PinLength)
        {
            return;
        }

        _entered += digit;
        Dots = ToDots(_entered.Length);
        Error = null;
        if (_entered.Length < AppLock.PinLength)
        {
            return;
        }

        _isChecking = true;
        try
        {
            var (matched, attemptsLeft) = await appLock.CheckPinAsync(_entered);
            if (matched)
            {
                _done.TrySetResult(true);
                return;
            }

            if (attemptsLeft == 0)
            {
                await SignOutAsync();
                return;
            }

            _entered = "";
            Dots = ToDots(0);
            Error = attemptsLeft == 1
                ? "Wrong PIN. One attempt left before you are signed out."
                : $"Wrong PIN. {attemptsLeft} attempts left.";
        }
        finally
        {
            _isChecking = false;
        }
    }

    [RelayCommand]
    private void Backspace()
    {
        if (_isChecking || _entered.Length == 0)
        {
            return;
        }

        _entered = _entered[..^1];
        Dots = ToDots(_entered.Length);
    }

    /// <summary>The way out for a forgotten PIN: signing out removes the lock.</summary>
    [RelayCommand]
    private async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        _done.TrySetResult(false);
    }

    private static string ToDots(int filled) =>
        string.Join(' ', Enumerable.Range(0, AppLock.PinLength).Select(i => i < filled ? '●' : '○'));
}
