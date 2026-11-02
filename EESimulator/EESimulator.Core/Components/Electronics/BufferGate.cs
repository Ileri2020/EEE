using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Unity-gain digital buffer (signal driver / level-restorer).
/// Output matches the digital logic state of the input.
/// </summary>
public class BufferComponent : ElectronicComponent
{
    public const string StableType = "electronics.buffer";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Buffer";
    public override string SpicePrefix => "BUF";
    public override string Description => "Unity-gain digital buffer (signal driver)";

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

    public BufferComponent()
    {
        BitWidth = 1;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
    }

    public BufferComponent(string name) : this()
    {
        Name = name;
        Reference = name;
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
        DigitalPinLayout.Replace(this, pins);
    }
}
