namespace EEE;

public partial class AppShell : Shell
{
    public AppShell(WorkspacePage workspacePage)
    {
        InitializeComponent();
        // Workspace, Settings, Help, and Examples are root ShellContent routes
        // declared in AppShell.xaml; registering them again as detail routes
        // makes absolute navigation ambiguous.
    }
}
