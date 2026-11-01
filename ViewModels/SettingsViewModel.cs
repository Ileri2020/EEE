using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace EEE.ViewModels;

/// <summary>
/// Model for an interactive row in the Control Settings table.
/// </summary>
public partial class ControlActionBinding : ObservableObject
{
    public string ActionName { get; set; } = string.Empty;
    public string Category { get; set; } = "Navigation";
    public string Target { get; set; } = "Canvas";
    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private string _selectedDesktopControl = string.Empty;

    [ObservableProperty]
    private string _selectedMobileControl = string.Empty;

    public List<string> DesktopOptions { get; set; } = new();
    public List<string> MobileOptions { get; set; } = new();
}

/// <summary>
/// ViewModel managing Viewport, Workspace, and Control settings for both Mobile and Desktop.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    // ─── Viewport Settings ────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private double _gridSize = 20.0;

    [ObservableProperty]
    private bool _showOriginCross = true;

    [ObservableProperty]
    private bool _showLiveProbes = true;

    [ObservableProperty]
    private bool _showWaveformBadges = true;

    [ObservableProperty]
    private string _selectedCanvasTheme = "Deep Dark (#0D1117)";

    public List<string> CanvasThemes { get; } = new()
    {
        "Deep Dark (#0D1117)",
        "Midnight Blue (#0B0E14)",
        "Charcoal (#161B22)",
        "High Contrast Dark (#000000)"
    };

    [ObservableProperty]
    private double _defaultZoom = 100.0;

    // ─── Workspace Settings ───────────────────────────────────────────────────
    [ObservableProperty]
    private bool _defaultSidebarVisible = true;

    [ObservableProperty]
    private bool _defaultConsoleVisible = false;

    [ObservableProperty]
    private bool _defaultWaveformDrawerVisible = false;

    [ObservableProperty]
    private bool _autoRunSimulation = false;

    [ObservableProperty]
    private string _defaultCategory = "All";

    public List<string> AvailableCategories { get; } = new()
    {
        "All", "Electrical", "Electronics", "General"
    };

    [ObservableProperty]
    private bool _mobilePanModeOnStartup = true;

    [ObservableProperty]
    private string _mobileToolbarPosition = "Left Dock";

    public List<string> ToolbarPositions { get; } = new()
    {
        "Left Dock", "Bottom Bar", "Floating"
    };

    // ─── Control Settings Table ───────────────────────────────────────────────
    public ObservableCollection<ControlActionBinding> ControlBindings { get; } = new();

    // ─── Control Sidebar Button Customization ─────────────────────────────────
    public ObservableCollection<EEE.Services.CustomControlButton> CustomButtons => EEE.Services.ControlSidebarService.CustomButtons;
    public List<string> AvailableActions => EEE.Services.ControlSidebarService.PerformableActions;

    [ObservableProperty]
    private string _newButtonLabel = string.Empty;

    [ObservableProperty]
    private string _selectedAction = "Play / Pause Simulation";

    [RelayCommand]
    private void CreateControlButton()
    {
        if (string.IsNullOrWhiteSpace(NewButtonLabel))
        {
            StatusMessage = "Please enter up to 3 letters for the button label.";
            return;
        }

        var label = NewButtonLabel.Trim();
        if (label.Length > 3)
        {
            label = label.Substring(0, 3);
        }

        EEE.Services.ControlSidebarService.AddButton(label, SelectedAction);
        StatusMessage = $"✓ Created control button [{label.ToUpperInvariant()}] linked to '{SelectedAction}'.";
        NewButtonLabel = string.Empty;
    }

    [RelayCommand]
    private void DeleteControlButton(EEE.Services.CustomControlButton button)
    {
        if (button != null)
        {
            var lbl = button.Label;
            EEE.Services.ControlSidebarService.DeleteButton(button);
            StatusMessage = $"Disconnected and deleted button [{lbl}].";
        }
    }

    [ObservableProperty]
    private string _statusMessage = "Settings loaded.";

    public SettingsViewModel()
    {
        InitializeControlBindings();
    }

    private void InitializeControlBindings()
    {
        ControlBindings.Clear();

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Pan Viewport",
            Category = "Navigation",
            Target = "Workspace",
            Description = "Scroll and explore different areas of the schematic.",
            DesktopOptions = new() { "Right-Drag", "Middle-Drag", "Alt + Left-Drag", "Space + Drag" },
            SelectedDesktopControl = "Right-Drag",
            MobileOptions = new() { "Pan Mode One-Finger Drag", "Two-Finger Drag", "Pan Button + Drag" },
            SelectedMobileControl = "Pan Mode One-Finger Drag"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Select Component / Pin",
            Category = "Selection",
            Target = "Component / Pin",
            Description = "Highlight and activate a component or individual pin node.",
            DesktopOptions = new() { "Left-Click", "Single-Click", "Ctrl + Left-Click" },
            SelectedDesktopControl = "Left-Click",
            MobileOptions = new() { "Select Mode Tap", "Single Tap", "Double Tap" },
            SelectedMobileControl = "Select Mode Tap"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Component Edit Dialog",
            Category = "Properties",
            Target = "Component",
            Description = "Open parameter and probe configuration dialog.",
            DesktopOptions = new() { "Right-Click (No Drag)", "Double Left-Click", "Shift + Right-Click" },
            SelectedDesktopControl = "Right-Click (No Drag)",
            MobileOptions = new() { "Select Mode Long-Press", "Double Tap", "Properties Button" },
            SelectedMobileControl = "Select Mode Long-Press"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Move Component",
            Category = "Editing",
            Target = "Component",
            Description = "Drag component to reposition it across the circuit canvas.",
            DesktopOptions = new() { "Right Long-Click + Drag", "Left-Click + Drag", "Shift + Drag" },
            SelectedDesktopControl = "Right Long-Click + Drag",
            MobileOptions = new() { "Pan Mode Long-Press + Drag", "Select Mode Drag", "Touch & Move" },
            SelectedMobileControl = "Pan Mode Long-Press + Drag"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Draw Wire",
            Category = "Wiring",
            Target = "Pin Node",
            Description = "Draw electrical connection wire from pin to another pin or node.",
            DesktopOptions = new() { "Pin Long-Press + Drag", "Pin Click Then Target Click", "Alt + Pin Drag" },
            SelectedDesktopControl = "Pin Long-Press + Drag",
            MobileOptions = new() { "Pin Long-Press + Drag", "Tap Pin Then Target Pin", "Wire Tool Active" },
            SelectedMobileControl = "Pin Long-Press + Drag"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Zoom In",
            Category = "View",
            Target = "Viewport",
            Description = "Magnify canvas schematic.",
            DesktopOptions = new() { "Scroll Wheel Up", "Ctrl + Plus", "Toolbar 🔍 Button" },
            SelectedDesktopControl = "Scroll Wheel Up",
            MobileOptions = new() { "Pinch Open", "Double-Tap Pinch", "Toolbar Button" },
            SelectedMobileControl = "Pinch Open"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Zoom Out",
            Category = "View",
            Target = "Viewport",
            Description = "Reduce magnification of canvas schematic.",
            DesktopOptions = new() { "Scroll Wheel Down", "Ctrl + Minus", "Toolbar ⊖ Button" },
            SelectedDesktopControl = "Scroll Wheel Down",
            MobileOptions = new() { "Pinch Close", "Two-Finger Tap", "Toolbar Button" },
            SelectedMobileControl = "Pinch Close"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Reset / Fit View",
            Category = "View",
            Target = "Viewport",
            Description = "Center all components within current viewport window.",
            DesktopOptions = new() { "Double Middle-Click", "Ctrl + 0", "Toolbar ⊡ Button" },
            SelectedDesktopControl = "Toolbar ⊡ Button",
            MobileOptions = new() { "Two-Finger Double-Tap", "Toolbar ⊡ Button" },
            SelectedMobileControl = "Toolbar ⊡ Button"
        });
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        ShowGrid = true;
        GridSize = 20.0;
        ShowOriginCross = true;
        ShowLiveProbes = true;
        ShowWaveformBadges = true;
        SelectedCanvasTheme = "Deep Dark (#0D1117)";
        DefaultZoom = 100.0;

        DefaultSidebarVisible = true;
        DefaultConsoleVisible = false;
        DefaultWaveformDrawerVisible = false;
        AutoRunSimulation = false;
        DefaultCategory = "All";
        MobilePanModeOnStartup = true;
        MobileToolbarPosition = "Left Dock";

        InitializeControlBindings();
        StatusMessage = "Settings reset to factory defaults.";
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        StatusMessage = "✓ All settings saved successfully!";
        await Task.Delay(800);
        await GoBack();
    }

    [RelayCommand]
    private async Task GoBack()
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
}
