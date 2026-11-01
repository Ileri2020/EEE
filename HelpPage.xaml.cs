using System;
using Microsoft.Maui.Controls;

namespace EEE;

public partial class HelpPage : ContentPage
{
    public HelpPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        catch
        {
            // fallback
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("settings");
        }
        catch
        {
            // fallback
        }
    }
}
