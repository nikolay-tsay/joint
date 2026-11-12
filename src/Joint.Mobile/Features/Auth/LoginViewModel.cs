using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Joint.Mobile.Features.Auth;

public partial class LoginViewModel(AuthService auth) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [RelayCommand]
    private async Task SignInWithGoogleAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            await auth.SignInWithGoogleAsync();
            await Shell.Current.GoToAsync(Routes.Home);
        }
        catch (TaskCanceledException)
        {
            // The user closed the browser; stay on the login page.
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            Error = "Sign-in failed. Check your connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
