namespace EEE;

public partial class AppShell : Shell
{
    public AppShell(WorkspacePage workspacePage)
    {
        InitializeComponent();
        // WorkspacePage is already declared as ShellContent in XAML,
        // but we also inject and add it here for DI resolution.
        Routing.RegisterRoute("workspace", typeof(WorkspacePage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("help", typeof(HelpPage));
    }
}
