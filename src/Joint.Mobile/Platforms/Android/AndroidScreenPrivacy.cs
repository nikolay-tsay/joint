using Android.Views;
using Joint.Mobile.Features.Lock;

namespace Joint.Mobile;

/// <summary>
/// Blanks the app's card in the recent-apps screen. Android 13 and later can do just
/// that; older versions only offer FLAG_SECURE, which also blocks screenshots.
/// </summary>
public class AndroidScreenPrivacy : IScreenPrivacy
{
    public void SetHidden(bool hidden) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Platform.CurrentActivity is not { } activity)
            {
                return;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                activity.SetRecentsScreenshotEnabled(!hidden);
            }
            else if (hidden)
            {
                activity.Window?.AddFlags(WindowManagerFlags.Secure);
            }
            else
            {
                activity.Window?.ClearFlags(WindowManagerFlags.Secure);
            }
        });
}
