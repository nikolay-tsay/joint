namespace Joint.Mobile.Features.Lock;

/// <summary>The device's fingerprint or face check.</summary>
public interface IBiometrics
{
    /// <summary>True when the device has biometrics and the user has enrolled one.</summary>
    bool IsAvailable { get; }

    /// <summary>Shows the system prompt. False when it fails, is cancelled or is unavailable.</summary>
    Task<bool> AuthenticateAsync(string title, string cancelText);
}
