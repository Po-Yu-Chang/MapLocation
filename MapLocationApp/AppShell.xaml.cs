using MapLocationApp.Views;

namespace MapLocationApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute(nameof(GeofenceManagementPage), typeof(GeofenceManagementPage));
	}
}
