using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Android.Runtime;
using Joint.Mobile.Features.Lock;

namespace Joint.Mobile;

/// <summary>
/// Android's own biometric prompt, used straight from the platform so no extra package is
/// needed. It needs Android 11; older devices simply fall back to the PIN.
/// </summary>
public class AndroidBiometrics : IBiometrics
{
    /// <summary>BiometricManager.Authenticators.BIOMETRIC_WEAK: any enrolled fingerprint or face.</summary>
    private const int AnyBiometric = 255;

    public bool IsAvailable =>
        OperatingSystem.IsAndroidVersionAtLeast(30)
        && Platform.AppContext.GetSystemService(Context.BiometricService) is BiometricManager manager
        && (int)manager.CanAuthenticate(AnyBiometric) == 0;

    public Task<bool> AuthenticateAsync(string title, string cancelText)
    {
        if (!IsAvailable
            || !OperatingSystem.IsAndroidVersionAtLeast(30)
            || Platform.CurrentActivity is not { MainExecutor: { } executor } activity)
        {
            return Task.FromResult(false);
        }

        var result = new TaskCompletionSource<bool>();
        var prompt = new BiometricPrompt.Builder(activity)
            .SetTitle(title)
            .SetNegativeButton(cancelText, executor, new CancelListener(result))
            .Build();
        prompt.Authenticate(new CancellationSignal(), executor, new Callback(result));
        return result.Task;
    }

    private sealed class Callback(TaskCompletionSource<bool> result) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? authenticationResult) =>
            result.TrySetResult(true);

        // A finger that was not recognized arrives as OnAuthenticationFailed and the prompt
        // stays open; this is the prompt giving up or being dismissed.
        public override void OnAuthenticationError(
            [GeneratedEnum] BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString) =>
            result.TrySetResult(false);
    }

    private sealed class CancelListener(TaskCompletionSource<bool> result)
        : Java.Lang.Object, IDialogInterfaceOnClickListener
    {
        public void OnClick(IDialogInterface? dialog, int which) => result.TrySetResult(false);
    }
}
