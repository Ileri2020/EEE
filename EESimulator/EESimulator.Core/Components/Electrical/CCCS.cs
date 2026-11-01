using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Current-Controlled Current Source (CCCS).
/// SPICE Prefix: F.
/// I_out = Beta * I_sense
/// </summary>
public class CCCSComponent : ElectricalComponent
{
    public const string StableType = "electrical.cccs";

    public override string StableTypeId => StableType;
    public override string ComponentType => "CCCS";
    public override string SpicePrefix => "F";
    public override string Description => "Current-Controlled Current Source (F)";

    public double CurrentGain
    {
        get => ElectricalParameters.TryGetValue("CurrentGain", out var val) ? val : 1.0;
        set => ElectricalParameters["CurrentGain"] = value;
    }

    public string ControllingSource
    {
        get => CustomProperties.TryGetValue("ControllingSource", out var val) ? val?.ToString() ?? "" : "";
        set => CustomProperties["ControllingSource"] = value;
    }

    public CCCSComponent()
    {
        // Output terminals (Right)
        Pins.Add(new ComponentPin("out_pos", "+", 20, -10));
        Pins.Add(new ComponentPin("out_neg", "−", 20, 10));

        // Sense current branch terminals (Left)
        Pins.Add(new ComponentPin("ctrl_pos", "IN+", -20, -10));
        Pins.Add(new ComponentPin("ctrl_neg", "IN−", -20, 10));

        CurrentGain = 1.0;
    }

    public CCCSComponent(string name, double currentGain = 1.0, string controllingSource = "") : this()
    {
        Name = name;
        Reference = name;
        CurrentGain = currentGain;
        ControllingSource = controllingSource;
    }
}
