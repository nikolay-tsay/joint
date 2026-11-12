namespace Joint.Mobile.Config;

/// <summary>
/// Where the app points. Debug builds use the development environment, Release builds
/// production. None of these values are secret: the Supabase key is the publishable one.
/// </summary>
public static class AppConfig
{
#if DEBUG
    public const string SupabaseUrl = "https://jwxmpsxepoazhjdyktdj.supabase.co";
    public const string SupabasePublishableKey = "sb_publishable_vo1YJno0mxbUTzlqV18oHg_0vO5k_Fm";
    public const string ApiBaseUrl = "https://joint-api-dev.up.railway.app";
#else
    public const string SupabaseUrl = "https://eqxpqrskavonvrvvhqdg.supabase.co";
    public const string SupabasePublishableKey = "sb_publishable_xpnLuQFatZHXkJgUbABIuA_5OnfMGPf";
    public const string ApiBaseUrl = "https://joint-api.up.railway.app";
#endif

    /// <summary>
    /// Where Supabase sends the browser back after sign-in. Must be listed under
    /// Redirect URLs in Supabase and match the intent filter on
    /// <c>AuthCallbackActivity</c>.
    /// </summary>
    public const string AuthCallbackScheme = "joint";
    public const string AuthCallbackUrl = AuthCallbackScheme + "://auth-callback";
}
