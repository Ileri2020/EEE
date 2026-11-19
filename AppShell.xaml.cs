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

    /// <summary>
    /// Attaches an already instantiated and inflated WorkspacePage directly to the Shell content,
    /// eliminating deferred template instantiation and preventing blank screen delays on Android.
    /// </summary>
    public void AttachWorkspacePage(WorkspacePage page)
    {
        WorkspaceShellContent.Content = page;
    }
}
