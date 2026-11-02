using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

public enum PullResistorMode
{
    PullUp,   // 1 (pull to high rail)
    PullDown, // 0 (pull to ground)
    Floating  // Z (tri-state / high-Z)
}

/// <summary>
/// Configurable Pull-up / Pull-down Resistor.
/// Connects a high-value resistor between the target signal pin and either a positive rail (+VCC)
/// or ground (0V) to prevent floating/high-Z inputs from oscillating.
/// </summary>
public class PullResistorComponent : ElectricalComponent
{
    public const string StableType = "electrical.pullresistor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "PullResistor";
    public override string SpicePrefix => "RPULL";
    public override string Description => "Configurable Pull-Up / Pull-Down Resistor";

    public PullResistorMode Mode
    {
        get
        {
            if (CustomProperties.TryGetValue("Mode", out var val) && val is PullResistorMode m)
                return m;
            if (CustomProperties.TryGetValue("Mode", out var strVal) && Enum.TryParse<PullResistorMode>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return PullResistorMode.PullUp;
        }
        set
        {
            CustomProperties["Mode"] = value;
            NotifyModified(nameof(Mode));
        }
    }

    /// <summary>Resistance in Ohms (Ω). Default is 10,000 Ω (10 kΩ).</summary>
    public double Resistance
    {
        get => ElectricalParameters.TryGetValue("Resistance", out var val) ? val : 10000.0;
        set
        {
            ElectricalParameters["Resistance"] = Math.Max(value, 1.0);
            NotifyModified(nameof(Resistance));
        }
    }

    /// <summary>Target rail voltage for pull-up mode (default 5.0 V).</summary>
    public double RailVoltage
    {
        get => ElectricalParameters.TryGetValue("RailVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["RailVoltage"] = value;
            NotifyModified(nameof(RailVoltage));
        }
    }

    public PullResistorComponent()
    {
        // Signal pin and optional custom rail pin
        Pins.Add(new ComponentPin("pin", "R", 0, 15));
        Pins.Add(new ComponentPin("rail", "RAIL", 0, -15));
        Resistance = 10000.0;
        RailVoltage = 5.0;
        Mode = PullResistorMode.PullUp;
    }

    public PullResistorComponent(string name, PullResistorMode mode = PullResistorMode.PullUp, double resistance = 10000.0) : this()
    {
        Name = name;
        Reference = name;
        Mode = mode;
        Resistance = resistance;
    }
}
