namespace EEE;

public partial class AppShell : Shell
{
    public AppShell(WorkspacePage workspacePage)
    {
        InitializeComponent();
        // Keep Settings available as a DI-created detail page so workspace
        // navigation does not land on a blank root ShellContent surface.
        Routing.RegisterRoute("settings-detail", typeof(SettingsPage));
        Routing.RegisterRoute("account-detail", typeof(AccountPage));
    }
}
