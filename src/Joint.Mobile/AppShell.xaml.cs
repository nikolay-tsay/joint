using Joint.Mobile.Features.Transactions;

namespace Joint.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Pushed on top of the tabs when an account is opened from Home.
		Routing.RegisterRoute(Routes.AccountTransactions, typeof(TransactionsPage));
	}
}
