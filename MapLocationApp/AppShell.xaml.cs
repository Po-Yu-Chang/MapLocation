using MapLocationApp.Views;

namespace MapLocationApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute(nameof(GeofenceManagementPage), typeof(GeofenceManagementPage));
		Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
		Routing.RegisterRoute(nameof(EditCheckInPage), typeof(EditCheckInPage));
	}
}
