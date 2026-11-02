using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Tri-state digital buffer with control enable input.
/// When enable is asserted, Output = Input; otherwise high impedance (floating).
/// </summary>
public class ControlledBufferComponent : ElectronicComponent
{
    public const string StableType = "electronics.controlledbuffer";

    public override string StableTypeId => StableType;
    public override string ComponentType => "ControlledBuffer";
    public override string SpicePrefix => "TRI";
    public override string Description => "Tri-state controlled buffer (enable input)";

    public bool ControlActiveLow
    {
        get => CustomProperties.TryGetValue("ControlActiveLow", out var val) && Convert.ToBoolean(val);
        set
        {
            CustomProperties["ControlActiveLow"] = value;
            NotifyModified(nameof(ControlActiveLow));
        }
    }

    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            RebuildPins();
            NotifyModified(nameof(BitWidth));
        }
    }

    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    public ControlledBufferComponent()
    {
        BitWidth = 1;
        ControlActiveLow = false;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
    }

    public ControlledBufferComponent(string name, bool activeLow = false) : this()
    {
        Name = name;
        Reference = name;
        ControlActiveLow = activeLow;
    }

    private void RebuildPins()
    {
        int width = BitWidth;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int bit = 0; bit < width; bit++)
        {
            string suffix = width == 1 ? string.Empty : bit.ToString(System.Globalization.CultureInfo.InvariantCulture);
            pins.Add(new ComponentPin(width == 1 ? "in" : $"in{suffix}", width == 1 ? "IN" : $"I{bit}", -24, DigitalPinLayout.Position(bit, width)));
            pins.Add(new ComponentPin(width == 1 ? "out" : $"out{suffix}", width == 1 ? "OUT" : $"O{bit}", 24, DigitalPinLayout.Position(bit, width)));
        }
        pins.Add(new ComponentPin("ctrl", ControlActiveLow ? "EN̅" : "EN", 0, 22));
        DigitalPinLayout.Replace(this, pins);
    }
}
