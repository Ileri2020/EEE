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
        set
        {
            ElectricalParameters["Aol"] = Math.Max(value, 1.0);
            NotifyModified(nameof(OpenLoopGain));
        }
    }

    public double GainBandwidth
    {
        get => ElectricalParameters.TryGetValue("GBW", out var val) ? val : 1e6;
        set
        {
            ElectricalParameters["GBW"] = Math.Max(value, 1.0);
            NotifyModified(nameof(GainBandwidth));
        }
    }

    public double SlewRate
    {
        get => ElectricalParameters.TryGetValue("SR", out var val) ? val : 0.5;
        set
        {
            ElectricalParameters["SR"] = Math.Max(value, 1e-6);
            NotifyModified(nameof(SlewRate));
        }
    }

    public double SupplyPositive
    {
        get => ElectricalParameters.TryGetValue("VccPlus", out var val) ? val : 15.0;
        set
        {
            ElectricalParameters["VccPlus"] = value;
            NotifyModified(nameof(SupplyPositive));
        }
    }

    public double SupplyNegative
    {
        get => ElectricalParameters.TryGetValue("VccMinus", out var val) ? val : -15.0;
        set
        {
            ElectricalParameters["VccMinus"] = value;
            NotifyModified(nameof(SupplyNegative));
        }
    }

    /// <summary>Input offset voltage in Volts (V). Default 1 mV (1e-3 V).</summary>
    public double InputOffsetVoltage
    {
        get => ElectricalParameters.TryGetValue("Vos", out var val) ? val : 1e-3;
        set
        {
            ElectricalParameters["Vos"] = value;
            NotifyModified(nameof(InputOffsetVoltage));
        }
    }

    /// <summary>Input bias current in Amperes (A). Default 80 nA (80e-9 A).</summary>
    public double InputBiasCurrent
    {
        get => ElectricalParameters.TryGetValue("Ib", out var val) ? val : 80e-9;
        set
        {
            ElectricalParameters["Ib"] = Math.Max(value, 0.0);
            NotifyModified(nameof(InputBiasCurrent));
        }
    }

    /// <summary>Common-mode rejection ratio in dB (default 90 dB).</summary>
    public double CMRR
    {
        get => ElectricalParameters.TryGetValue("CMRR", out var val) ? val : 90.0;
        set
        {
            ElectricalParameters["CMRR"] = Math.Max(value, 0.0);
            NotifyModified(nameof(CMRR));
        }
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
        InputOffsetVoltage = 1e-3;
        InputBiasCurrent = 80e-9;
        CMRR = 90.0;
        PartNumber = "LM741";
    }

    public OpAmpComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
