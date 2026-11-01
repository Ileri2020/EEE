using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Voltage-Controlled Current Source (VCCS).
/// SPICE Prefix: G.
/// I_out = Gm * (V_in+ - V_in-)
/// </summary>
public class VCCSComponent : ElectricalComponent
{
    public const string StableType = "electrical.vccs";

    public override string StableTypeId => StableType;
    public override string ComponentType => "VCCS";
    public override string SpicePrefix => "G";
    public override string Description => "Voltage-Controlled Current Source (G)";

    public double Transconductance
    {
        get => ElectricalParameters.TryGetValue("Transconductance", out var val) ? val : 0.001;
        set => ElectricalParameters["Transconductance"] = value;
    }

    public VCCSComponent()
    {
        // Output terminals (Right)
        Pins.Add(new ComponentPin("out_pos", "+", 20, -10));
        Pins.Add(new ComponentPin("out_neg", "−", 20, 10));

        // Controlling input terminals (Left)
        Pins.Add(new ComponentPin("ctrl_pos", "IN+", -20, -10));
        Pins.Add(new ComponentPin("ctrl_neg", "IN−", -20, 10));

        Transconductance = 0.001;
    }

    public VCCSComponent(string name, double transconductance = 0.001) : this()
    {
        Name = name;
        Reference = name;
        Transconductance = transconductance;
    }
}
