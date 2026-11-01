using EEE.ViewModels;
using Microsoft.Maui.Controls;

namespace EEE;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
