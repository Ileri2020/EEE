using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Voltage-Controlled Voltage Source (VCVS).
/// SPICE Prefix: E.
/// V_out = Gain * (V_in+ - V_in-)
/// </summary>
public class VCVSComponent : ElectricalComponent
{
    public const string StableType = "electrical.vcvs";

    public override string StableTypeId => StableType;
    public override string ComponentType => "VCVS";
    public override string SpicePrefix => "E";
    public override string Description => "Voltage-Controlled Voltage Source (E)";

    public double VoltageGain
    {
        get => ElectricalParameters.TryGetValue("Gain", out var val) ? val : 1.0;
        set => ElectricalParameters["Gain"] = value;
    }

    public VCVSComponent()
    {
        // Output terminals (Right)
        Pins.Add(new ComponentPin("out_pos", "+", 20, -10));
        Pins.Add(new ComponentPin("out_neg", "−", 20, 10));

        // Controlling input terminals (Left)
        Pins.Add(new ComponentPin("ctrl_pos", "IN+", -20, -10));
        Pins.Add(new ComponentPin("ctrl_neg", "IN−", -20, 10));

        VoltageGain = 1.0;
    }

    public VCVSComponent(string name, double gain = 1.0) : this()
    {
        Name = name;
        Reference = name;
        VoltageGain = gain;
    }
}
