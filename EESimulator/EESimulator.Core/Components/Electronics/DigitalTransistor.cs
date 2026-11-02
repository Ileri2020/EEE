using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum DigitalTransistorType
{
    NType, // Closed when Gate is HIGH
    PType  // Closed when Gate is LOW
}

public enum GateLocation
{
    TopRight,
    BottomLeft
}

/// <summary>
/// Digital-Mode Switch Transistor.
/// Operates as an ideal voltage-controlled bilateral switch between Source and Drain:
/// - N-Type: Conducts (Ron) when Gate is HIGH (> Threshold); High-Z (Roff) when LOW.
/// - P-Type: Conducts (Ron) when Gate is LOW (< Threshold); High-Z (Roff) when HIGH.
/// Simpler and faster to simulate in digital circuits than analog MOSFETs/BJTs.
/// </summary>
public class DigitalTransistorComponent : ElectronicComponent
{
    public const string StableType = "electronics.transistor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Transistor";
    public override string SpicePrefix => "SWT";
    public override string Description => "Digital-Mode Switch Transistor (N-Type / P-Type)";

    public DigitalTransistorType TransistorType
    {
        get
        {
            if (CustomProperties.TryGetValue("TransistorType", out var val) && val is DigitalTransistorType t)
                return t;
            if (CustomProperties.TryGetValue("TransistorType", out var strVal) && Enum.TryParse<DigitalTransistorType>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return DigitalTransistorType.NType;
        }
        set
        {
            CustomProperties["TransistorType"] = value;
            NotifyModified(nameof(TransistorType));
        }
    }

    /// <summary>Logisim Gate / Control Pin Location: TopRight or BottomLeft (StdAttr.SELECT_LOC).</summary>
    public GateLocation GateLocation
    {
        get
        {
            if (CustomProperties.TryGetValue("GateLocation", out var val) && val is GateLocation g)
                return g;
            if (CustomProperties.TryGetValue("GateLocation", out var strVal) && Enum.TryParse<GateLocation>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return GateLocation.TopRight;
        }
        set
        {
            CustomProperties["GateLocation"] = value;
            NotifyModified(nameof(GateLocation));
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

    /// <summary>On-state channel resistance in Ohms (Ω). Default 0.1 Ω.</summary>
    public double ResistanceOn
    {
        get => ElectricalParameters.TryGetValue("Ron", out var val) ? val : 0.1;
        set
        {
            ElectricalParameters["Ron"] = Math.Max(value, 1e-4);
            NotifyModified(nameof(ResistanceOn));
        }
    }

    /// <summary>Off-state channel resistance in Ohms (Ω). Default 10 MΩ.</summary>
    public double ResistanceOff
    {
        get => ElectricalParameters.TryGetValue("Roff", out var val) ? val : 10e6;
        set
        {
            ElectricalParameters["Roff"] = Math.Max(value, 100.0);
            NotifyModified(nameof(ResistanceOff));
        }
    }

    /// <summary>Switching voltage threshold (V). Default 2.5 V.</summary>
    public double Threshold
    {
        get => ElectricalParameters.TryGetValue("Threshold", out var val) ? val : 2.5;
        set
        {
            ElectricalParameters["Threshold"] = value;
            NotifyModified(nameof(Threshold));
        }
    }

    public DigitalTransistorComponent()
    {
        // 3 pins: Drain (top), Source (bottom), Gate (left control)
        Pins.Add(new ComponentPin("drain", "D", 0, -20));
        Pins.Add(new ComponentPin("source", "S", 0, 20));
        Pins.Add(new ComponentPin("gate", "G", -20, 0));

        TransistorType = DigitalTransistorType.NType;
        ResistanceOn = 0.1;
        ResistanceOff = 10e6;
        Threshold = 2.5;
    }

    public DigitalTransistorComponent(string name, DigitalTransistorType type = DigitalTransistorType.NType) : this()
    {
        Name = name;
        Reference = name;
        TransistorType = type;
    }
}
