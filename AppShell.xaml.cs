using Microsoft.Extensions.DependencyInjection;

namespace EEE;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        WorkspaceShellContent.ContentTemplate = new DataTemplate(
            () => services.GetRequiredService<WorkspacePage>());
        SettingsShellContent.ContentTemplate = new DataTemplate(
            () => services.GetRequiredService<SettingsPage>());
        Routing.RegisterRoute("account-detail", typeof(AccountPage));
    }
}
