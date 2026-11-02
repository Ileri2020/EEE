using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Wired Logic Constant Value Source.
/// Emits a fixed, continuous logic level (HIGH / 1 or LOW / 0) or configurable integer/voltage value.
/// </summary>
public class ConstantComponent : ElectronicComponent
{
    public const string StableType = "electronics.constant";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Constant";
    public override string Description => "Wired Logic Constant Source (HIGH / LOW / Value)";

    /// <summary>Constant integer logic value (e.g. 0 or 1, or bus constant).</summary>
    public long Value
    {
        get => ElectricalParameters.TryGetValue("Value", out var val) ? (long)val : 1;
        set
        {
            ElectricalParameters["Value"] = value;
            LiveVoltage = value > 0 ? VoltageHigh : VoltageLow;
            NotifyModified(nameof(Value));
        }
    }

    /// <summary>Logisim Bit Width (1 to 32 bits, default 1).</summary>
    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            NotifyModified(nameof(BitWidth));
        }
    }

    /// <summary>Voltage corresponding to Logic HIGH (default 5.0 V).</summary>
    public double VoltageHigh
    {
        get => ElectricalParameters.TryGetValue("VoltageHigh", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["VoltageHigh"] = value;
            if (Value > 0) LiveVoltage = value;
            NotifyModified(nameof(VoltageHigh));
        }
    }

    /// <summary>Voltage corresponding to Logic LOW (default 0.0 V).</summary>
    public double VoltageLow
    {
        get => ElectricalParameters.TryGetValue("VoltageLow", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["VoltageLow"] = value;
            if (Value == 0) LiveVoltage = value;
            NotifyModified(nameof(VoltageLow));
        }
    }

    public ConstantComponent()
    {
        Pins.Add(new ComponentPin("out", "VAL", 15, 0));
        Value = 1;
        VoltageHigh = 5.0;
        VoltageLow = 0.0;
        LiveVoltage = 5.0;
    }

    public ConstantComponent(string name, long value = 1) : this()
    {
        Name = name;
        Reference = name;
        Value = value;
    }
}
