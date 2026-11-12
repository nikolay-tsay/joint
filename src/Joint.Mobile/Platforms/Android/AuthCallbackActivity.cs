using Android.App;
using Android.Content;
using Android.Content.PM;
using Joint.Mobile.Config;

namespace Joint.Mobile;

/// <summary>Receives the browser redirect that ends a sign-in and hands it back to the app.</summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = AppConfig.AuthCallbackScheme)]
public class AuthCallbackActivity : WebAuthenticatorCallbackActivity
{
}
