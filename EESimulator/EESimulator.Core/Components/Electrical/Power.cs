using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Positive Supply Rail (+VCC / VDD) node.
/// Supplies a constant positive DC voltage rail (e.g. +5.0 V, +3.3 V, +12.0 V) referenced to circuit ground (node 0).
/// </summary>
public class PowerComponent : ElectricalComponent
{
    public const string StableType = "electrical.power";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Power";
    public override string SpicePrefix => "VCC";
    public override string Description => "Positive Supply Rail (+VCC / VDD)";

    /// <summary>Rail voltage (V). Default is +5.0 V.</summary>
    public double Voltage
    {
        get => ElectricalParameters.TryGetValue("Voltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["Voltage"] = value;
            LiveVoltage = value;
            NotifyModified(nameof(Voltage));
        }
    }

    /// <summary>Logisim Bit Width (legacy digital leftover, guarded for backwards compatibility).</summary>
    [Obsolete("BitWidth is a Logisim digital leftover and is not used in analog circuit simulation.")]
    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            NotifyModified(nameof(BitWidth));
        }
    }

    public PowerComponent()
    {
        // Terminal at bottom so the arrow points up
        Pins.Add(new ComponentPin("vcc", "+VCC", 0, 15));
        Voltage = 5.0;
    }

    public PowerComponent(string name, double voltage = 5.0) : this()
    {
        Name = name;
        Reference = name;
        Voltage = voltage;
    }
}
