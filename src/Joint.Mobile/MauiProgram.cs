using Joint.Mobile.Api;
using Joint.Mobile.Features.Auth;
using Joint.Mobile.Features.Home;
using Joint.Mobile.Features.Lock;
using Joint.Mobile.Features.Profile;
using Joint.Mobile.Features.Start;
using Joint.Mobile.Features.Transactions;
using Microsoft.Extensions.Logging;

namespace Joint.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<SupabaseAuthClient>();
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<ApiClient>();
		builder.Services.AddSingleton<AppLock>();
		builder.Services.AddSingleton<LockScreen>();
		builder.Services.AddSingleton<IBiometrics, AndroidBiometrics>();

		builder.Services.AddTransient<StartPage>();
		builder.Services.AddTransient<LockPage>();
		builder.Services.AddTransient<LockViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<HomeViewModel>();
		builder.Services.AddTransient<TransactionsPage>();
		builder.Services.AddTransient<TransactionsViewModel>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<ProfileViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
