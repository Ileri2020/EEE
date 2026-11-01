using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Storage;

namespace EEE.Services;

/// <summary>
/// Model for a user-customized action button placed in the Control Sidebar.
/// Displays a max 3-letter label and binds to an app action.
/// </summary>
public partial class CustomControlButton : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _label = "ACT"; // Max 3 characters

    [ObservableProperty]
    private string _actionName = "Play / Pause Simulation";

    [ObservableProperty]
    private string _category = "General";

    [ObservableProperty]
    private string _description = string.Empty;

    // User-authored description field (§3.E / §4.E)
    [ObservableProperty]
    private string _userDescription = string.Empty;

    public List<string> AvailableActions => ControlSidebarService.PerformableActions;

    // Serialization-friendly DTO
    internal ButtonDto ToDto() => new()
    {
        Id = Id,
        Label = Label,
        ActionName = ActionName,
        Category = Category,
        Description = Description,
        UserDescription = UserDescription
    };

    internal static CustomControlButton FromDto(ButtonDto dto) => new()
    {
        Id = dto.Id,
        Label = dto.Label,
        ActionName = dto.ActionName,
        Category = dto.Category,
        Description = dto.Description,
        UserDescription = dto.UserDescription
    };
}

/// <summary>Plain-data DTO used for JSON persistence of CustomControlButton.</summary>
internal class ButtonDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "ACT";
    public string ActionName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string UserDescription { get; set; } = string.Empty;
}

/// <summary>
/// Singleton service managing custom control buttons for the Control Sidebar.
/// Shared between SettingsPage and WorkspacePage. Persists buttons to Preferences on every change (§3.A).
/// </summary>
public static class ControlSidebarService
{
    private const string PrefKey = "ControlSidebar.CustomButtons.v1";

    public static ObservableCollection<CustomControlButton> CustomButtons { get; } = new();

    /// <summary>
    /// Full list of actions that can be assigned to a Control Sidebar button.
    /// Grouped logically; includes all major workspace actions (§3.F).
    /// </summary>
    public static readonly List<string> PerformableActions = new()
    {
        // Simulation
        "Play / Pause Simulation",
        "Run Operating Point",

        // Navigation
        "Pan Mode",
        "Move Component",

        // Selection & Editing
        "Select Mode",
        "Component Edit Dialog",
        "Rotate Selected",

        // Wiring
        "Draw Wire",

        // View
        "Zoom In",
        "Zoom Out",
        "Reset / Fit View",

        // Edit history
        "Undo",
        "Redo",

        // Panels & overlays
        "Toggle Component Palette",
        "Toggle Waveform Drawer",
        "Toggle Control Sidebar",
        "Toggle Description Sidebar",
        "Toggle Script / Schematic",

        // Script
        "Run Script",

        // Project
        "Save Project",
        "Clear Console",

        // Help
        "Open Help",
        "Open Settings",
    };

    static ControlSidebarService()
    {
        // Attempt to restore persisted buttons; fall back to defaults on failure (§3.A)
        if (!TryLoadFromPreferences())
        {
            PopulateDefaults();
        }
    }

    // ── Persistence ────────────────────────────────────────────────────────────

    private static bool TryLoadFromPreferences()
    {
        try
        {
            string? json = Preferences.Default.Get<string?>(PrefKey, null);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var dtos = JsonSerializer.Deserialize<List<ButtonDto>>(json);
            if (dtos == null || dtos.Count == 0)
                return false;

            CustomButtons.Clear();
            foreach (var dto in dtos)
                CustomButtons.Add(CustomControlButton.FromDto(dto));

            // Subscribe to change notifications for each loaded button
            SubscribeToChanges(CustomButtons);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveToPreferences()
    {
        try
        {
            var dtos = CustomButtons.Select(b => b.ToDto()).ToList();
            string json = JsonSerializer.Serialize(dtos);
            Preferences.Default.Set(PrefKey, json);
        }
        catch
        {
            // Silently ignore persistence failures (read-only FS, etc.)
        }
    }

    private static void SubscribeToChanges(IEnumerable<CustomControlButton> buttons)
    {
        foreach (var btn in buttons)
        {
            btn.PropertyChanged += (_, _) => SaveToPreferences();
        }
    }

    // ── Default buttons ─────────────────────────────────────────────────────────

    private static void PopulateDefaults()
    {
        CustomButtons.Clear();

        var defaults = new[]
        {
            new CustomControlButton { Label = "SIM", ActionName = "Play / Pause Simulation", Category = "Simulation",  Description = "Toggle circuit electricity flow simulation" },
            new CustomControlButton { Label = "PAN", ActionName = "Pan Mode",                 Category = "Navigation",  Description = "Switch to viewport panning mode" },
            new CustomControlButton { Label = "SEL", ActionName = "Select Mode",              Category = "Selection",   Description = "Switch to component & pin select mode" },
            new CustomControlButton { Label = "WIR", ActionName = "Draw Wire",                Category = "Wiring",      Description = "Activate wire connection tool" },
            new CustomControlButton { Label = "EDT", ActionName = "Component Edit Dialog",    Category = "Properties",  Description = "Open parameter dialog for selected component" },
            new CustomControlButton { Label = "FIT", ActionName = "Reset / Fit View",         Category = "View",        Description = "Fit all components into view" },
            new CustomControlButton { Label = "ZIN", ActionName = "Zoom In",                  Category = "View",        Description = "Zoom in closer to schematic" },
            new CustomControlButton { Label = "ZOT", ActionName = "Zoom Out",                 Category = "View",        Description = "Zoom out viewport" },
            new CustomControlButton { Label = "UND", ActionName = "Undo",                     Category = "Edit",        Description = "Undo previous circuit command" },
            new CustomControlButton { Label = "RED", ActionName = "Redo",                     Category = "Edit",        Description = "Redo reverted circuit command" },
        };

        foreach (var btn in defaults)
            CustomButtons.Add(btn);

        SubscribeToChanges(CustomButtons);
        SaveToPreferences();
    }

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new button. Label is clamped to 3 chars by the UI Entry MaxLength; this method
    /// applies no further truncation (dead-code truncation removed per §3.E). Returns false if
    /// the label is empty.
    /// </summary>
    public static bool AddButton(string rawLabel, string actionName, string userDescription = "")
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
            return false;

        var label = rawLabel.Trim().ToUpperInvariant();

        var btn = new CustomControlButton
        {
            Label = label,
            ActionName = actionName,
            Category = GetCategoryForAction(actionName),
            Description = $"Perform {actionName}",
            UserDescription = userDescription
        };

        btn.PropertyChanged += (_, _) => SaveToPreferences();
        CustomButtons.Add(btn);
        SaveToPreferences();
        return true;
    }

    public static void DeleteButton(CustomControlButton button)
    {
        if (button != null && CustomButtons.Remove(button))
            SaveToPreferences();
    }

    public static string GetCategoryForAction(string action) => action switch
    {
        "Play / Pause Simulation" or "Run Operating Point" or "Run Script" => "Simulation",
        "Pan Mode" or "Move Component"                                      => "Navigation",
        "Select Mode"                                                       => "Selection",
        "Draw Wire"                                                         => "Wiring",
        "Component Edit Dialog"                                             => "Properties",
        "Zoom In" or "Zoom Out" or "Reset / Fit View"                      => "View",
        "Undo" or "Redo"                                                    => "Edit",
        "Save Project" or "Clear Console"                                   => "Project",
        "Toggle Component Palette" or "Toggle Waveform Drawer"
            or "Toggle Control Sidebar" or "Toggle Description Sidebar"
            or "Toggle Script / Schematic"                                  => "Panels",
        "Rotate Selected"                                                   => "Edit",
        "Open Help" or "Open Settings"                                      => "General",
        _                                                                   => "General"
    };

    /// <summary>Returns action names grouped by category for Settings page display (§3.B).</summary>
    public static IEnumerable<(string Category, List<string> Actions)> GetGroupedActions()
    {
        return PerformableActions
            .GroupBy(GetCategoryForAction)
            .Select(g => (g.Key, g.ToList()));
    }
}
