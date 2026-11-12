using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Start;

/// <summary>
/// Shown at launch while the saved session is checked. It repeats the system splash
/// (same logo, same place, same background) so the hand-over is not visible.
/// </summary>
public class StartPage : ContentPage
{
    private readonly AuthService _auth;

    public StartPage(AuthService auth)
    {
        _auth = auth;
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
        var signedIn = await _auth.TryRestoreAsync();
        await Shell.Current.GoToAsync(signedIn ? Routes.Home : Routes.Login);
    }
}
