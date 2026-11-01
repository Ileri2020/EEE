using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

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
/// Changes to toggles apply immediately and auto-persist to Preferences (§4.B).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    // ─── Settings Tab Navigation ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTabControls))]
    [NotifyPropertyChangedFor(nameof(IsTabViewport))]
    [NotifyPropertyChangedFor(nameof(IsTabWorkspace))]
    private string _activeSettingsTab = "Controls";

    public bool IsTabControls  => ActiveSettingsTab == "Controls";
    public bool IsTabViewport  => ActiveSettingsTab == "Viewport";
    public bool IsTabWorkspace => ActiveSettingsTab == "Workspace";

    [RelayCommand]
    private void SwitchTab(string tab) => ActiveSettingsTab = tab;

    // ─── Viewport Settings ────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _showGrid = true;

    partial void OnShowGridChanged(bool value) => PersistBool(nameof(ShowGrid), value);

    [ObservableProperty]
    private double _gridSize = 20.0;

    partial void OnGridSizeChanged(double value) => PersistDouble(nameof(GridSize), value);

    [ObservableProperty]
    private bool _showOriginCross = true;

    partial void OnShowOriginCrossChanged(bool value) => PersistBool(nameof(ShowOriginCross), value);

    [ObservableProperty]
    private bool _showLiveProbes = true;

    partial void OnShowLiveProbesChanged(bool value) => PersistBool(nameof(ShowLiveProbes), value);

    [ObservableProperty]
    private bool _showWaveformBadges = true;

    partial void OnShowWaveformBadgesChanged(bool value) => PersistBool(nameof(ShowWaveformBadges), value);

    [ObservableProperty]
    private string _selectedCanvasTheme = "Deep Dark (#0D1117)";

    partial void OnSelectedCanvasThemeChanged(string value) => PersistString(nameof(SelectedCanvasTheme), value);

    public List<string> CanvasThemes { get; } = new()
    {
        "Deep Dark (#0D1117)",
        "Midnight Blue (#0B0E14)",
        "Charcoal (#161B22)",
        "High Contrast Dark (#000000)"
    };

    [ObservableProperty]
    private double _defaultZoom = 100.0;

    partial void OnDefaultZoomChanged(double value) => PersistDouble(nameof(DefaultZoom), value);

    // ─── Workspace Settings ───────────────────────────────────────────────────
    [ObservableProperty]
    private bool _defaultSidebarVisible = true;

    partial void OnDefaultSidebarVisibleChanged(bool value) => PersistBool(nameof(DefaultSidebarVisible), value);

    [ObservableProperty]
    private bool _defaultConsoleVisible = false;

    partial void OnDefaultConsoleVisibleChanged(bool value) => PersistBool(nameof(DefaultConsoleVisible), value);

    [ObservableProperty]
    private bool _defaultWaveformDrawerVisible = false;

    partial void OnDefaultWaveformDrawerVisibleChanged(bool value) => PersistBool(nameof(DefaultWaveformDrawerVisible), value);

    [ObservableProperty]
    private bool _autoRunSimulation = false;

    partial void OnAutoRunSimulationChanged(bool value) => PersistBool(nameof(AutoRunSimulation), value);

    [ObservableProperty]
    private string _defaultCategory = "All";

    partial void OnDefaultCategoryChanged(string value) => PersistString(nameof(DefaultCategory), value);

    public List<string> AvailableCategories { get; } = new()
    {
        "All", "Electrical", "Electronics", "General"
    };

    [ObservableProperty]
    private bool _mobilePanModeOnStartup = true;

    partial void OnMobilePanModeOnStartupChanged(bool value) => PersistBool(nameof(MobilePanModeOnStartup), value);

    [ObservableProperty]
    private string _mobileToolbarPosition = "Left Dock";

    partial void OnMobileToolbarPositionChanged(string value) => PersistString(nameof(MobileToolbarPosition), value);

    public List<string> ToolbarPositions { get; } = new()
    {
        "Left Dock", "Bottom Bar", "Floating"
    };

    // ─── Control Settings Table ───────────────────────────────────────────────
    public ObservableCollection<ControlActionBinding> ControlBindings { get; } = new();

    // ─── Control Sidebar Button Customization ─────────────────────────────────
    public ObservableCollection<EEE.Services.CustomControlButton> CustomButtons
        => EEE.Services.ControlSidebarService.CustomButtons;

    public List<string> AvailableActions
        => EEE.Services.ControlSidebarService.PerformableActions;

    /// <summary>Actions grouped by category for the Settings picker (§3.B).</summary>
    public IEnumerable<(string Category, List<string> Actions)> GroupedActions
        => EEE.Services.ControlSidebarService.GetGroupedActions();

    [ObservableProperty]
    private string _newButtonLabel = string.Empty;

    [ObservableProperty]
    private string _selectedAction = "Play / Pause Simulation";

    /// <summary>Character count hint shown beside the label Entry (§3.E).</summary>
    public string LabelCharCount => $"({NewButtonLabel?.Length ?? 0}/3)";

    partial void OnNewButtonLabelChanged(string value) =>
        OnPropertyChanged(nameof(LabelCharCount));

    [RelayCommand]
    private void CreateControlButton()
    {
        if (string.IsNullOrWhiteSpace(NewButtonLabel))
        {
            ShowToast("Please enter up to 3 letters for the button label.");
            return;
        }

        EEE.Services.ControlSidebarService.AddButton(NewButtonLabel.Trim(), SelectedAction);
        ShowToast($"✓ Created button [{NewButtonLabel.Trim().ToUpperInvariant()}] → '{SelectedAction}'.");
        NewButtonLabel = string.Empty;
    }

    [RelayCommand]
    private void DeleteControlButton(EEE.Services.CustomControlButton button)
    {
        if (button != null)
        {
            var lbl = button.Label;
            EEE.Services.ControlSidebarService.DeleteButton(button);
            ShowToast($"Deleted button [{lbl}].");
        }
    }

    // ─── Status / Toast ───────────────────────────────────────────────────────

    /// <summary>
    /// Transient toast message shown in the bottom status bar only (not duplicated in the header — §4.D).
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isToastVisible = false;

    private async void ShowToast(string message)
    {
        StatusMessage = message;
        IsToastVisible = true;
        await Task.Delay(2500);
        IsToastVisible = false;
        StatusMessage = string.Empty;
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    public SettingsViewModel()
    {
        LoadFromPreferences();
        InitializeControlBindings();
    }

    // ─── Persistence helpers ──────────────────────────────────────────────────

    private static void PersistBool(string key, bool value)
        => Preferences.Default.Set($"Settings.{key}", value);

    private static void PersistDouble(string key, double value)
        => Preferences.Default.Set($"Settings.{key}", value);

    private static void PersistString(string key, string value)
        => Preferences.Default.Set($"Settings.{key}", value);

    private void LoadFromPreferences()
    {
        ShowGrid                   = Preferences.Default.Get($"Settings.{nameof(ShowGrid)}",                   true);
        GridSize                   = Preferences.Default.Get($"Settings.{nameof(GridSize)}",                   20.0);
        ShowOriginCross            = Preferences.Default.Get($"Settings.{nameof(ShowOriginCross)}",            true);
        ShowLiveProbes             = Preferences.Default.Get($"Settings.{nameof(ShowLiveProbes)}",             true);
        ShowWaveformBadges         = Preferences.Default.Get($"Settings.{nameof(ShowWaveformBadges)}",        true);
        SelectedCanvasTheme        = Preferences.Default.Get($"Settings.{nameof(SelectedCanvasTheme)}",       "Deep Dark (#0D1117)");
        DefaultZoom                = Preferences.Default.Get($"Settings.{nameof(DefaultZoom)}",               100.0);
        DefaultSidebarVisible      = Preferences.Default.Get($"Settings.{nameof(DefaultSidebarVisible)}",     true);
        DefaultConsoleVisible      = Preferences.Default.Get($"Settings.{nameof(DefaultConsoleVisible)}",     false);
        DefaultWaveformDrawerVisible = Preferences.Default.Get($"Settings.{nameof(DefaultWaveformDrawerVisible)}", false);
        AutoRunSimulation          = Preferences.Default.Get($"Settings.{nameof(AutoRunSimulation)}",         false);
        DefaultCategory            = Preferences.Default.Get($"Settings.{nameof(DefaultCategory)}",           "All");
        MobilePanModeOnStartup     = Preferences.Default.Get($"Settings.{nameof(MobilePanModeOnStartup)}",    true);
        MobileToolbarPosition      = Preferences.Default.Get($"Settings.{nameof(MobileToolbarPosition)}",     "Left Dock");
    }

    // ─── Control Bindings ─────────────────────────────────────────────────────

    private void InitializeControlBindings()
    {
        ControlBindings.Clear();

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Pan Viewport", Category = "Navigation", Target = "Workspace",
            Description = "Scroll and explore different areas of the schematic.",
            DesktopOptions = new() { "Right-Drag", "Middle-Drag", "Alt + Left-Drag", "Space + Drag" },
            SelectedDesktopControl = "Right-Drag",
            MobileOptions = new() { "Pan Mode One-Finger Drag", "Two-Finger Drag", "Pan Button + Drag" },
            SelectedMobileControl = "Pan Mode One-Finger Drag"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Select Component / Pin", Category = "Selection", Target = "Component / Pin",
            Description = "Highlight and activate a component or individual pin node.",
            DesktopOptions = new() { "Left-Click", "Single-Click", "Ctrl + Left-Click" },
            SelectedDesktopControl = "Left-Click",
            MobileOptions = new() { "Select Mode Tap", "Single Tap", "Double Tap" },
            SelectedMobileControl = "Select Mode Tap"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Component Edit Dialog", Category = "Properties", Target = "Component",
            Description = "Open parameter and probe configuration dialog.",
            DesktopOptions = new() { "Right-Click (No Drag)", "Double Left-Click", "Shift + Right-Click" },
            SelectedDesktopControl = "Right-Click (No Drag)",
            MobileOptions = new() { "Select Mode Long-Press", "Double Tap", "Properties Button" },
            SelectedMobileControl = "Select Mode Long-Press"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Move Component", Category = "Editing", Target = "Component",
            Description = "Drag component to reposition it across the circuit canvas.",
            DesktopOptions = new() { "Right Long-Click + Drag", "Left-Click + Drag", "Shift + Drag" },
            SelectedDesktopControl = "Right Long-Click + Drag",
            MobileOptions = new() { "Pan Mode Long-Press + Drag", "Select Mode Drag", "Touch & Move" },
            SelectedMobileControl = "Pan Mode Long-Press + Drag"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Draw Wire", Category = "Wiring", Target = "Pin Node",
            Description = "Draw electrical connection wire from pin to another pin or node.",
            DesktopOptions = new() { "Pin Long-Press + Drag", "Pin Click Then Target Click", "Alt + Pin Drag" },
            SelectedDesktopControl = "Pin Long-Press + Drag",
            MobileOptions = new() { "Pin Long-Press + Drag", "Tap Pin Then Target Pin", "Wire Tool Active" },
            SelectedMobileControl = "Pin Long-Press + Drag"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Zoom In", Category = "View", Target = "Viewport",
            Description = "Magnify canvas schematic.",
            DesktopOptions = new() { "Scroll Wheel Up", "Ctrl + Plus", "Toolbar 🔍 Button" },
            SelectedDesktopControl = "Scroll Wheel Up",
            MobileOptions = new() { "Pinch Open", "Double-Tap Pinch", "Toolbar Button" },
            SelectedMobileControl = "Pinch Open"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Zoom Out", Category = "View", Target = "Viewport",
            Description = "Reduce magnification of canvas schematic.",
            DesktopOptions = new() { "Scroll Wheel Down", "Ctrl + Minus", "Toolbar ⊖ Button" },
            SelectedDesktopControl = "Scroll Wheel Down",
            MobileOptions = new() { "Pinch Close", "Two-Finger Tap", "Toolbar Button" },
            SelectedMobileControl = "Pinch Close"
        });

        ControlBindings.Add(new ControlActionBinding
        {
            ActionName = "Reset / Fit View", Category = "View", Target = "Viewport",
            Description = "Center all components within current viewport window.",
            DesktopOptions = new() { "Double Middle-Click", "Ctrl + 0", "Toolbar ⊡ Button" },
            SelectedDesktopControl = "Toolbar ⊡ Button",
            MobileOptions = new() { "Two-Finger Double-Tap", "Toolbar ⊡ Button" },
            SelectedMobileControl = "Toolbar ⊡ Button"
        });
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets all settings to factory defaults.
    /// Wrapped with a confirmation dialog to prevent accidental data loss (§4.C).
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaults()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Reset to Defaults",
            "This will delete all custom buttons and reset all settings to factory defaults. Continue?",
            "Reset",
            "Cancel");

        if (!confirmed)
            return;

        // Clear all persisted preferences
        Preferences.Default.Clear();

        // Restore in-memory values to defaults (each OnXChanged partial will re-persist)
        ShowGrid                    = true;
        GridSize                    = 20.0;
        ShowOriginCross             = true;
        ShowLiveProbes              = true;
        ShowWaveformBadges          = true;
        SelectedCanvasTheme         = "Deep Dark (#0D1117)";
        DefaultZoom                 = 100.0;
        DefaultSidebarVisible       = true;
        DefaultConsoleVisible       = false;
        DefaultWaveformDrawerVisible = false;
        AutoRunSimulation           = false;
        DefaultCategory             = "All";
        MobilePanModeOnStartup      = true;
        MobileToolbarPosition       = "Left Dock";

        InitializeControlBindings();
        ShowToast("Settings reset to factory defaults.");
    }

    [RelayCommand]
    private async Task GoBack()
    {
        try
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        catch { /* fallback */ }
    }
}
