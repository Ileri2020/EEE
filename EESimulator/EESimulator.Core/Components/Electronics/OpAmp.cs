using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Operational Amplifier component.
/// </summary>
public class OpAmpComponent : ElectronicComponent
{
    public const string StableType = "electronics.opamp";

    public override string StableTypeId => StableType;
    public override string ComponentType => "OpAmp";
    public override string Description => "Operational Amplifier (5-terminal model)";

    public double OpenLoopGain
    {
        get => ElectricalParameters.TryGetValue("Aol", out var val) ? val : 100000.0;
        set => ElectricalParameters["Aol"] = value;
    }

    public OpAmpComponent()
    {
        // 5 pins: Inverting (-), Non-inverting (+), Output, V+, V-
        Pins.Add(new ComponentPin("in_neg", "In-", -25, -10));
        Pins.Add(new ComponentPin("in_pos", "In+", -25, 10));
        Pins.Add(new ComponentPin("out", "Out", 25, 0));
        Pins.Add(new ComponentPin("v_pos", "V+", 0, -25));
        Pins.Add(new ComponentPin("v_neg", "V-", 0, 25));
        OpenLoopGain = 100000.0;
        PartNumber = "LM741";
    }

    public OpAmpComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
