using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Current-Controlled Switch.
/// SPICE Prefix: W.
/// Switches between Ron and Roff based on current through reference branch.
/// </summary>
public class CurrentSwitchComponent : ElectricalComponent
{
    public const string StableType = "electrical.currentswitch";

    public override string StableTypeId => StableType;
    public override string ComponentType => "CurrentSwitch";
    public override string SpicePrefix => "W";
    public override string Description => "Current-Controlled Switch (W)";

    public double ResistanceOn
    {
        get => ElectricalParameters.TryGetValue("Ron", out var val) ? val : 1.0;
        set => ElectricalParameters["Ron"] = value;
    }

    public double ResistanceOff
    {
        get => ElectricalParameters.TryGetValue("Roff", out var val) ? val : 1e6;
        set => ElectricalParameters["Roff"] = value;
    }

    public double Threshold
    {
        get => ElectricalParameters.TryGetValue("Threshold", out var val) ? val : 0.001;
        set => ElectricalParameters["Threshold"] = value;
    }

    public double Hysteresis
    {
        get => ElectricalParameters.TryGetValue("Hysteresis", out var val) ? val : 0.0;
        set => ElectricalParameters["Hysteresis"] = value;
    }

    public string ControllingSource
    {
        get => CustomProperties.TryGetValue("ControllingSource", out var val) ? val?.ToString() ?? "" : "";
        set => CustomProperties["ControllingSource"] = value;
    }

    public CurrentSwitchComponent()
    {
        // Switch switched terminals (horizontal)
        Pins.Add(new ComponentPin("sw_pos", "S+", -20, 0));
        Pins.Add(new ComponentPin("sw_neg", "S−", 20, 0));

        // Sense current branch terminals (vertical)
        Pins.Add(new ComponentPin("ctrl_pos", "IN+", 0, -20));
        Pins.Add(new ComponentPin("ctrl_neg", "IN−", 0, 20));

        ResistanceOn = 1.0;
        ResistanceOff = 1e6;
        Threshold = 0.001;
        Hysteresis = 0.0;
    }

    public CurrentSwitchComponent(string name, double ron = 1.0, double roff = 1e6, double threshold = 0.001, double hysteresis = 0.0, string controllingSource = "") : this()
    {
        Name = name;
        Reference = name;
        ResistanceOn = ron;
        ResistanceOff = roff;
        Threshold = threshold;
        Hysteresis = hysteresis;
        ControllingSource = controllingSource;
    }
}
