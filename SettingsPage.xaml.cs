using EEE.ViewModels;
using Microsoft.Maui.Controls;

namespace EEE;

public partial class SettingsPage : ContentPage
{
    private readonly EEWorkspaceViewModel _workspace;

    public SettingsPage(SettingsViewModel vm, EEWorkspaceViewModel workspace)
    {
        InitializeComponent();
        BindingContext = vm;
        _workspace = workspace;
    }

    private async void OnOpenControlSidebarClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//workspace");
        _workspace.ToggleControlSidebarCommand.Execute(null);
    }
}
