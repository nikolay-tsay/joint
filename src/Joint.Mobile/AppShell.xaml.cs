using Joint.Mobile.Features.Guide;
using Joint.Mobile.Features.Lock;
using Joint.Mobile.Features.Transactions;

namespace Joint.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Pushed on top of the tabs when an account is opened from Home.
		Routing.RegisterRoute(Routes.AccountTransactions, typeof(TransactionsPage));

		// Pushed from Profile.
		Routing.RegisterRoute(Routes.Guide, typeof(GuidePage));
		Routing.RegisterRoute(Routes.Security, typeof(LockSettingsPage));
	}
}
