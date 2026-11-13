using Joint.Mobile.Features.Lock;

namespace Joint.Mobile;

public partial class App : Application
{
	private readonly LockScreen _lockScreen;

	public App(LockScreen lockScreen)
	{
		InitializeComponent();
		_lockScreen = lockScreen;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());

		// The app lock: note when the app leaves the screen, and ask again when it has been away a while.
		window.Stopped += (_, _) => _lockScreen.NoteBackgrounded();
		window.Resumed += async (_, _) =>
		{
			if (!await _lockScreen.OnResumedAsync())
			{
				await Shell.Current.GoToAsync(Routes.Login);
			}
		};

		return window;
	}
}