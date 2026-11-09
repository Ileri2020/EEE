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
/// ViewModel managing viewport and workspace settings.
/// Changes to toggles apply immediately and auto-persist to Preferences (§4.B).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private bool _loadingRemoteApiBaseUrl;
    // ─── Settings Tab Navigation ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTabControls))]
    [NotifyPropertyChangedFor(nameof(IsTabViewport))]
    [NotifyPropertyChangedFor(nameof(IsTabWorkspace))]
    public partial string ActiveSettingsTab { get; set; } = "Controls";

    public bool IsTabControls  => ActiveSettingsTab == "Controls";
    public bool IsTabViewport  => ActiveSettingsTab == "Viewport";
    public bool IsTabWorkspace => ActiveSettingsTab == "Workspace";

    [RelayCommand]
    private void SwitchTab(string tab) => ActiveSettingsTab = tab;

    // ─── Viewport Settings ────────────────────────────────────────────────────
    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    partial void OnShowGridChanged(bool value) => PersistBool(nameof(ShowGrid), value);

    [ObservableProperty]
    public partial double GridSize { get; set; } = 20.0;

    partial void OnGridSizeChanged(double value) => PersistDouble(nameof(GridSize), value);

    [ObservableProperty]
    public partial bool ShowOriginCross { get; set; } = true;

    partial void OnShowOriginCrossChanged(bool value) => PersistBool(nameof(ShowOriginCross), value);

    [ObservableProperty]
    public partial bool ShowLiveProbes { get; set; } = true;

    partial void OnShowLiveProbesChanged(bool value) => PersistBool(nameof(ShowLiveProbes), value);

    [ObservableProperty]
    public partial bool ShowWaveformBadges { get; set; } = true;

    partial void OnShowWaveformBadgesChanged(bool value) => PersistBool(nameof(ShowWaveformBadges), value);

    [ObservableProperty]
    public partial string SelectedCanvasTheme { get; set; } = "Deep Dark (#0D1117)";

    partial void OnSelectedCanvasThemeChanged(string value) => PersistString(nameof(SelectedCanvasTheme), value);

    public List<string> CanvasThemes { get; } = new()
    {
        "Deep Dark (#0D1117)",
        "Midnight Blue (#0B0E14)",
        "Charcoal (#161B22)",
        "High Contrast Dark (#000000)"
    };

    [ObservableProperty]
    public partial double DefaultZoom { get; set; } = 100.0;

    partial void OnDefaultZoomChanged(double value) => PersistDouble(nameof(DefaultZoom), value);

    // ─── Workspace Settings ───────────────────────────────────────────────────
    [ObservableProperty]
    public partial bool DefaultSidebarVisible { get; set; } = true;

    partial void OnDefaultSidebarVisibleChanged(bool value) => PersistBool(nameof(DefaultSidebarVisible), value);

    [ObservableProperty]
    public partial bool DefaultConsoleVisible { get; set; } = false;

    partial void OnDefaultConsoleVisibleChanged(bool value) => PersistBool(nameof(DefaultConsoleVisible), value);

    [ObservableProperty]
    public partial bool DefaultWaveformDrawerVisible { get; set; } = false;

    partial void OnDefaultWaveformDrawerVisibleChanged(bool value) => PersistBool(nameof(DefaultWaveformDrawerVisible), value);

    [ObservableProperty]
    public partial bool AutoRunSimulation { get; set; } = false;

    partial void OnAutoRunSimulationChanged(bool value) => PersistBool(nameof(AutoRunSimulation), value);

    [ObservableProperty]
    public partial string DefaultCategory { get; set; } = "All";

    partial void OnDefaultCategoryChanged(string value) => PersistString(nameof(DefaultCategory), value);

    public List<string> AvailableCategories { get; } = new()
    {
        "All", "Electrical", "Electronics", "General", "Transmission Lines", "Electrical Machines",
        "Electrical Loads", "Sources & Energy", "Energy Storage"
    };

    [ObservableProperty] public partial bool EnableTimeScaling { get; set; }
    partial void OnEnableTimeScalingChanged(bool value) => PersistBool(nameof(EnableTimeScaling), value);

    [ObservableProperty] public partial string SimulationTimeUnit { get; set; } = "seconds";
    partial void OnSimulationTimeUnitChanged(string value) { PersistString(nameof(SimulationTimeUnit), value); OnPropertyChanged(nameof(TimeScalingRatio)); }

    [ObservableProperty] public partial string DeviceTimeUnit { get; set; } = "hours";
    partial void OnDeviceTimeUnitChanged(string value) { PersistString(nameof(DeviceTimeUnit), value); OnPropertyChanged(nameof(TimeScalingRatio)); }

    [ObservableProperty] public partial double SimulationTimeAmount { get; set; } = 30;
    partial void OnSimulationTimeAmountChanged(double value) { PersistDouble(nameof(SimulationTimeAmount), Math.Max(value, 1e-9)); OnPropertyChanged(nameof(TimeScalingRatio)); }

    [ObservableProperty] public partial double DeviceTimeAmount { get; set; } = 1;
    partial void OnDeviceTimeAmountChanged(double value) { PersistDouble(nameof(DeviceTimeAmount), Math.Max(value, 1e-9)); OnPropertyChanged(nameof(TimeScalingRatio)); }

    [ObservableProperty] public partial double SimulationTimeStepSeconds { get; set; } = 0.04;
    partial void OnSimulationTimeStepSecondsChanged(double value) => PersistDouble(nameof(SimulationTimeStepSeconds), Math.Max(value, 1e-6));

    [ObservableProperty] public partial bool AutomaticTimeStepSelection { get; set; } = true;
    partial void OnAutomaticTimeStepSelectionChanged(bool value) => PersistBool(nameof(AutomaticTimeStepSelection), value);

    [ObservableProperty] public partial double SimulationStartTimeSeconds { get; set; }
    partial void OnSimulationStartTimeSecondsChanged(double value) => PersistDouble(nameof(SimulationStartTimeSeconds), Math.Max(value, 0));

    [ObservableProperty] public partial double SimulationDurationSeconds { get; set; } = 60;
    partial void OnSimulationDurationSecondsChanged(double value) => PersistDouble(nameof(SimulationDurationSeconds), Math.Max(value, 1e-6));

    public double TimeScalingRatio =>
        Math.Max(DeviceTimeAmount, 1e-9) * TimeUnitSeconds(DeviceTimeUnit) /
        (Math.Max(SimulationTimeAmount, 1e-9) * TimeUnitSeconds(SimulationTimeUnit));

    public List<string> TimeUnits { get; } = new() { "seconds", "minutes", "hours", "days" };

    public static double TimeUnitSeconds(string unit) => unit?.ToLowerInvariant() switch
    {
        "minute" or "minutes" or "min" => 60,
        "hour" or "hours" or "hr" => 3600,
        "day" or "days" => 86400,
        _ => 1
    };

    [ObservableProperty]
    public partial string RemoteApiBaseUrl { get; set; } = string.Empty;

    partial void OnRemoteApiBaseUrlChanged(string value)
    {
        if (_loadingRemoteApiBaseUrl) return;
        var normalized = (value ?? string.Empty).Trim().TrimEnd('/');
        Preferences.Default.Set("Settings.RemoteApiBaseUrl", normalized);
        EEE.Services.RemoteEeeApiClient.ApiBaseUrl = normalized;
    }

    [ObservableProperty]
    public partial string RemoteApiAccessTokenDraft { get; set; } = string.Empty;

    [RelayCommand]
    private async Task SaveRemoteApiAccessTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(RemoteApiAccessTokenDraft))
        {
            ShowToast("Enter a user access token first.");
            return;
        }

        try
        {
            await SecureStorage.Default.SetAsync(
                EEE.Services.RemoteEeeApiClient.SecureAccessTokenKey,
                RemoteApiAccessTokenDraft.Trim());
            RemoteApiAccessTokenDraft = string.Empty;
            ShowToast("User access token saved securely on this device.");
        }
        catch (Exception ex)
        {
            ShowToast($"Could not save access token: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ClearRemoteApiAccessTokenAsync()
    {
        try
        {
            SecureStorage.Default.Remove(EEE.Services.RemoteEeeApiClient.SecureAccessTokenKey);
            ShowToast("Saved access token removed.");
        }
        catch (Exception ex)
        {
            ShowToast($"Could not remove access token: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenHcvpAccountAsync()
    {
        var baseUrl = EEE.Services.RemoteEeeApiClient.ApiBaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            ShowToast("Set the HCVP API base URL first.");
            return;
        }

        try
        {
            await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(new Uri(uri, "/account"));
        }
        catch (Exception ex)
        {
            ShowToast($"Could not open HCVP account: {ex.Message}");
        }
    }

    // ─── Control Sidebar Button Customization ─────────────────────────────────
    public ObservableCollection<EEE.Services.CustomControlButton> CustomButtons
        => EEE.Services.ControlSidebarService.CustomButtons;

    public List<string> AvailableActions
        => EEE.Services.ControlSidebarService.PerformableActions;

    /// <summary>Actions grouped by category for the Settings picker (§3.B).</summary>
    public IEnumerable<(string Category, List<string> Actions)> GroupedActions
        => EEE.Services.ControlSidebarService.GetGroupedActions();

    [ObservableProperty]
    public partial string NewButtonLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedAction { get; set; } = "Play / Pause Simulation";

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
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsToastVisible { get; set; } = false;

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
        EnableTimeScaling = Preferences.Default.Get($"Settings.{nameof(EnableTimeScaling)}", false);
        SimulationTimeUnit = Preferences.Default.Get($"Settings.{nameof(SimulationTimeUnit)}", "seconds");
        DeviceTimeUnit = Preferences.Default.Get($"Settings.{nameof(DeviceTimeUnit)}", "hours");
        SimulationTimeAmount = Preferences.Default.Get($"Settings.{nameof(SimulationTimeAmount)}", 30.0);
        DeviceTimeAmount = Preferences.Default.Get($"Settings.{nameof(DeviceTimeAmount)}", 1.0);
        SimulationTimeStepSeconds = Preferences.Default.Get($"Settings.{nameof(SimulationTimeStepSeconds)}", 0.04);
        AutomaticTimeStepSelection = Preferences.Default.Get($"Settings.{nameof(AutomaticTimeStepSelection)}", true);
        SimulationStartTimeSeconds = Preferences.Default.Get($"Settings.{nameof(SimulationStartTimeSeconds)}", 0.0);
        SimulationDurationSeconds = Preferences.Default.Get($"Settings.{nameof(SimulationDurationSeconds)}", 60.0);
        _loadingRemoteApiBaseUrl = true;
        RemoteApiBaseUrl = EEE.Services.RemoteEeeApiClient.ApiBaseUrl;
        _loadingRemoteApiBaseUrl = false;
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets all settings to factory defaults.
    /// Wrapped with a confirmation dialog to prevent accidental data loss (§4.C).
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaults()
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
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
