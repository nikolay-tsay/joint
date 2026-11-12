using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.Content;
using AndroidX.Core.View;

namespace Joint.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        MatchStatusBarToPage();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        // Switching between light and dark does not restart the activity, so re-apply.
        MatchStatusBarToPage();
    }

    /// <summary>
    /// Gives the status bar (clock, battery) the page's background color and makes its
    /// icons dark on a light page and light on a dark one.
    /// </summary>
    private void MatchStatusBarToPage()
    {
        if (Window is null)
        {
            return;
        }

        var isDark = (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;

#pragma warning disable CA1422 // Has no effect from Android 15, where the bar is always transparent.
        Window.SetStatusBarColor(new Android.Graphics.Color(
            ContextCompat.GetColor(this, Resource.Color.colorPrimaryDark)));
#pragma warning restore CA1422

        if (WindowCompat.GetInsetsController(Window, Window.DecorView) is { } insets)
        {
            insets.AppearanceLightStatusBars = !isDark;
        }
    }
}
