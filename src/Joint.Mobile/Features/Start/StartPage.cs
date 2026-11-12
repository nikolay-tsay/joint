using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Start;

/// <summary>Shown briefly at launch while the saved session is checked.</summary>
public class StartPage : ContentPage
{
    private readonly AuthService _auth;

    public StartPage(AuthService auth)
    {
        _auth = auth;
        Shell.SetNavBarIsVisible(this, false);
        Content = new ActivityIndicator
        {
            IsRunning = true,
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
