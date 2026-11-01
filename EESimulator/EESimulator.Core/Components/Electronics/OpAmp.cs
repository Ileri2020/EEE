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

    public double GainBandwidth
    {
        get => ElectricalParameters.TryGetValue("GBW", out var val) ? val : 1e6;
        set => ElectricalParameters["GBW"] = value;
    }

    public double SlewRate
    {
        get => ElectricalParameters.TryGetValue("SR", out var val) ? val : 0.5;
        set => ElectricalParameters["SR"] = value;
    }

    public double SupplyPositive
    {
        get => ElectricalParameters.TryGetValue("VccPlus", out var val) ? val : 15.0;
        set => ElectricalParameters["VccPlus"] = value;
    }

    public double SupplyNegative
    {
        get => ElectricalParameters.TryGetValue("VccMinus", out var val) ? val : -15.0;
        set => ElectricalParameters["VccMinus"] = value;
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
        GainBandwidth = 1e6;
        SlewRate = 0.5;
        SupplyPositive = 15.0;
        SupplyNegative = -15.0;
        PartNumber = "LM741";
    }

    public OpAmpComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
