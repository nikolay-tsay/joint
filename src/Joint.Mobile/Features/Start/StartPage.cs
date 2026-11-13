using Joint.Mobile.Features.Auth;
using Joint.Mobile.Features.Lock;

namespace Joint.Mobile.Features.Start;

/// <summary>
/// Shown at launch while the saved session is checked. It repeats the system splash
/// (same logo, same place, same background) so the hand-over is not visible.
/// </summary>
public class StartPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly LockScreen _lockScreen;
    private bool _started;

    public StartPage(AuthService auth, LockScreen lockScreen)
    {
        _auth = auth;
        _lockScreen = lockScreen;
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        this.SetAppThemeColor(
            BackgroundColorProperty,
            (Color)Application.Current!.Resources["PageBackground"],
            (Color)Application.Current.Resources["PageBackgroundDark"]);

        Content = new Image
        {
            Source = "logo.png",
            WidthRequest = 192,
            HeightRequest = 192,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Appearing fires again when the lock page closes over this one.
        if (_started)
        {
            return;
        }

        _started = true;

        // A saved session still has to get past the app lock, when that is on.
        var signedIn = await _auth.TryRestoreAsync() && await _lockScreen.RequireUnlockAsync();
        await Shell.Current.GoToAsync(signedIn ? Routes.Home : Routes.Login);
    }
}
