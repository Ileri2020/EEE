using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Current-Controlled Voltage Source (CCVS).
/// SPICE Prefix: H.
/// V_out = Rm * I_sense
/// </summary>
public class CCVSComponent : ElectricalComponent
{
    public const string StableType = "electrical.ccvs";

    public override string StableTypeId => StableType;
    public override string ComponentType => "CCVS";
    public override string SpicePrefix => "H";
    public override string Description => "Current-Controlled Voltage Source (H)";

    public double Transresistance
    {
        get => ElectricalParameters.TryGetValue("Transresistance", out var val) ? val : 1000.0;
        set => ElectricalParameters["Transresistance"] = value;
    }

    public string ControllingSource
    {
        get => CustomProperties.TryGetValue("ControllingSource", out var val) ? val?.ToString() ?? "" : "";
        set => CustomProperties["ControllingSource"] = value;
    }

    public CCVSComponent()
    {
        // Output terminals (Right)
        Pins.Add(new ComponentPin("out_pos", "+", 20, -10));
        Pins.Add(new ComponentPin("out_neg", "−", 20, 10));

        // Sense current branch terminals (Left)
        Pins.Add(new ComponentPin("ctrl_pos", "IN+", -20, -10));
        Pins.Add(new ComponentPin("ctrl_neg", "IN−", -20, 10));

        Transresistance = 1000.0;
    }

    public CCVSComponent(string name, double transresistance = 1000.0, string controllingSource = "") : this()
    {
        Name = name;
        Reference = name;
        Transresistance = transresistance;
        ControllingSource = controllingSource;
    }
}
