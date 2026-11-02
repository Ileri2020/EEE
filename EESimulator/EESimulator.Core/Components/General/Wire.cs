using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Schematic wire segment linking pins or junctions.
/// </summary>
public class WireComponent : GeneralComponent
{
    public const string StableType = "general.wire";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Wire";
    public override string Description => "Schematic wire connection";

    public double EndX { get; set; }
    public double EndY { get; set; }

    /// <summary>
    /// Non-negligible real parasitic resistance of the wire in Ohms (0 for ideal short circuit).
    /// </summary>
    public double Resistance
    {
        get => ElectricalParameters.TryGetValue("Resistance", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["Resistance"] = Math.Max(value, 0.0);
            NotifyModified(nameof(Resistance));
        }
    }

    /// <summary>
    /// Parasitic series inductance of the wire in Henries (H).
    /// </summary>
    public double Inductance
    {
        get => ElectricalParameters.TryGetValue("Inductance", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["Inductance"] = Math.Max(value, 0.0);
            NotifyModified(nameof(Inductance));
        }
    }

    /// <summary>
    /// Parasitic shunt capacitance to ground/shield in Farads (F).
    /// </summary>
    public double Capacitance
    {
        get => ElectricalParameters.TryGetValue("Capacitance", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["Capacitance"] = Math.Max(value, 0.0);
            NotifyModified(nameof(Capacitance));
        }
    }

    public WireComponent()
    {
        Pins.Add(new ComponentPin("start", "1", 0, 0));
        Pins.Add(new ComponentPin("end", "2", 40, 0));
    }

    public WireComponent(string name, double startX, double startY, double endX, double endY) : this()
    {
        Name = name;
        X = startX;
        Y = startY;
        EndX = endX;
        EndY = endY;
    }
}
