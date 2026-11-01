using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Voltage-Controlled Switch.
/// SPICE Prefix: S.
/// Switches smoothly between Ron and Roff based on control voltage (Von / Voff / Threshold / Hysteresis).
/// </summary>
public class VoltageSwitchComponent : ElectricalComponent
{
    public const string StableType = "electrical.voltageswitch";

    public override string StableTypeId => StableType;
    public override string ComponentType => "VoltageSwitch";
    public override string SpicePrefix => "S";
    public override string Description => "Voltage-Controlled Switch (S)";

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
        get => ElectricalParameters.TryGetValue("Threshold", out var val) ? val : 1.0;
        set => ElectricalParameters["Threshold"] = value;
    }

    public double Hysteresis
    {
        get => ElectricalParameters.TryGetValue("Hysteresis", out var val) ? val : 0.1;
        set => ElectricalParameters["Hysteresis"] = value;
    }

    public VoltageSwitchComponent()
    {
        // Switch switched terminals (horizontal)
        Pins.Add(new ComponentPin("sw_pos", "S+", -20, 0));
        Pins.Add(new ComponentPin("sw_neg", "S−", 20, 0));

        // Control terminals (vertical)
        Pins.Add(new ComponentPin("ctrl_pos", "IN+", 0, -20));
        Pins.Add(new ComponentPin("ctrl_neg", "IN−", 0, 20));

        ResistanceOn = 1.0;
        ResistanceOff = 1e6;
        Threshold = 1.0;
        Hysteresis = 0.1;
    }

    public VoltageSwitchComponent(string name, double ron = 1.0, double roff = 1e6, double threshold = 1.0, double hysteresis = 0.1) : this()
    {
        Name = name;
        Reference = name;
        ResistanceOn = ron;
        ResistanceOff = roff;
        Threshold = threshold;
        Hysteresis = hysteresis;
    }
}
