using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Voltage Delay Element.
/// SPICE Prefix: VD.
/// Drives an output to a delayed input voltage: V(out+, out-) = V(in+, in-)(t - Delay).
/// </summary>
public class VoltageDelayComponent : ElectricalComponent
{
    public const string StableType = "electrical.voltagedelay";

    public override string StableTypeId => StableType;
    public override string ComponentType => "VoltageDelay";
    public override string SpicePrefix => "VD";
    public override string Description => "Voltage Delay Element (Delay)";

    public double Delay
    {
        get => ElectricalParameters.TryGetValue("Delay", out var val) ? val : 1e-6;
        set => ElectricalParameters["Delay"] = value;
    }

    public VoltageDelayComponent()
    {
        // Output terminals (Right)
        Pins.Add(new ComponentPin("out_pos", "V+", 20, -10));
        Pins.Add(new ComponentPin("out_neg", "V−", 20, 10));

        // Controlling input terminals (Left)
        Pins.Add(new ComponentPin("in_pos", "VC+", -20, -10));
        Pins.Add(new ComponentPin("in_neg", "VC−", -20, 10));

        Delay = 1e-6;
    }

    public VoltageDelayComponent(string name, double delay = 1e-6) : this()
    {
        Name = name;
        Reference = name;
        Delay = delay;
    }
}
