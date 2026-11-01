using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

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

    public List<string> AvailableActions => ControlSidebarService.PerformableActions;
}

/// <summary>
/// Singleton service managing custom control buttons for the Control Sidebar.
/// Shared between SettingsPage and WorkspacePage.
/// </summary>
public static class ControlSidebarService
{
    public static ObservableCollection<CustomControlButton> CustomButtons { get; } = new();

    public static readonly List<string> PerformableActions = new()
    {
        "Play / Pause Simulation",
        "Run Operating Point",
        "Pan Mode",
        "Select Mode",
        "Component Edit Dialog",
        "Move Component",
        "Draw Wire",
        "Zoom In",
        "Zoom Out",
        "Reset / Fit View",
        "Undo",
        "Redo",
        "Toggle Component Palette",
        "Toggle Waveform Drawer",
        "Toggle Script / Schematic",
        "Open Help",
        "Open Settings"
    };

    static ControlSidebarService()
    {
        // Populate default 3-letter buttons
        CustomButtons.Add(new CustomControlButton
        {
            Label = "SIM",
            ActionName = "Play / Pause Simulation",
            Category = "Simulation",
            Description = "Toggle circuit electricity flow simulation"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "PAN",
            ActionName = "Pan Mode",
            Category = "Navigation",
            Description = "Switch to viewport panning mode"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "SEL",
            ActionName = "Select Mode",
            Category = "Selection",
            Description = "Switch to component & pin select mode"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "WIR",
            ActionName = "Draw Wire",
            Category = "Wiring",
            Description = "Activate wire connection tool"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "EDT",
            ActionName = "Component Edit Dialog",
            Category = "Properties",
            Description = "Open parameter dialog for selected component"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "FIT",
            ActionName = "Reset / Fit View",
            Category = "View",
            Description = "Fit all components into view"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "ZIN",
            ActionName = "Zoom In",
            Category = "View",
            Description = "Zoom in closer to schematic"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "ZOT",
            ActionName = "Zoom Out",
            Category = "View",
            Description = "Zoom out viewport"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "UND",
            ActionName = "Undo",
            Category = "Edit",
            Description = "Undo previous circuit command"
        });

        CustomButtons.Add(new CustomControlButton
        {
            Label = "RED",
            ActionName = "Redo",
            Category = "Edit",
            Description = "Redo reverted circuit command"
        });
    }

    public static bool AddButton(string rawLabel, string actionName)
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
            return false;

        // User can give each created button max of 3 letters name
        var clean = rawLabel.Trim();
        var label = clean.Length > 3 ? clean.Substring(0, 3).ToUpperInvariant() : clean.ToUpperInvariant();

        CustomButtons.Add(new CustomControlButton
        {
            Label = label,
            ActionName = actionName,
            Category = GetCategoryForAction(actionName),
            Description = $"Perform {actionName}"
        });
        return true;
    }

    public static void DeleteButton(CustomControlButton button)
    {
        if (button != null)
        {
            CustomButtons.Remove(button);
        }
    }

    public static string GetCategoryForAction(string action) => action switch
    {
        "Play / Pause Simulation" or "Run Operating Point" => "Simulation",
        "Pan Mode" or "Move Component" => "Navigation",
        "Select Mode" => "Selection",
        "Draw Wire" => "Wiring",
        "Component Edit Dialog" => "Properties",
        "Zoom In" or "Zoom Out" or "Reset / Fit View" => "View",
        "Undo" or "Redo" => "Edit",
        _ => "General"
    };
}
