using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Joint.Mobile.Features.Lock;

/// <summary>
/// The offer to turn the app lock on, shown once right after signing in. Declining is
/// fine: the lock can be turned on later from Profile.
/// </summary>
public partial class LockSetupViewModel(AppLock appLock, IBiometrics biometrics) : ObservableObject
{
    private readonly TaskCompletionSource _done = new();

    /// <summary>Completes when the user has turned the lock on or declined.</summary>
    public Task Done => _done.Task;

    [ObservableProperty]
    public partial string? NewPin { get; set; }

    [ObservableProperty]
    public partial string? RepeatPin { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Says what unlocking will look like on this phone.</summary>
    public string Explanation => biometrics.IsAvailable
        ? "Joint shows your household's money. With the app lock on, it asks for your fingerprint or face, "
            + "or a PIN, every time you open it, and hides its contents in the app switcher."
        : "Joint shows your household's money. With the app lock on, it asks for a PIN every time you open it, "
            + "and hides its contents in the app switcher.";

    [RelayCommand]
    private async Task TurnOnAsync()
    {
        if (NewPin is not { Length: AppLock.PinLength } pin || !pin.All(char.IsAsciiDigit))
        {
            Error = $"A PIN is {AppLock.PinLength} digits.";
            return;
        }

        if (pin != RepeatPin)
        {
            Error = "The two PINs don't match.";
            return;
        }

        await appLock.SetPinAsync(pin);

        // Biometrics are the quicker way in; the PIN stays as the fallback either way.
        if (biometrics.IsAvailable && await biometrics.AuthenticateAsync("Confirm it's you", "Use PIN only"))
        {
            await appLock.SetBiometricsAsync(true);
        }

        _done.TrySetResult();
    }

    [RelayCommand]
    private void Skip() => _done.TrySetResult();
}
