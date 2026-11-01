using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Modular semiconductor junction diode.
/// </summary>
public class DiodeComponent : ElectricalComponent
{
    public const string StableType = "electrical.diode";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Diode";
    public override string SpicePrefix => "D";
    public override string Description => "P-N semiconductor junction diode";

    public double SaturationCurrent
    {
        get => ElectricalParameters.TryGetValue("Is", out var val) ? val : 1e-14;
        set => ElectricalParameters["Is"] = value;
    }

    public double EmissionCoefficient
    {
        get => ElectricalParameters.TryGetValue("N", out var val) ? val : 1.0;
        set => ElectricalParameters["N"] = value;
    }

    public double BreakdownVoltage
    {
        get => ElectricalParameters.TryGetValue("Bv", out var val) ? val : 0.0;
        set => ElectricalParameters["Bv"] = value;
    }

    public double SeriesResistance
    {
        get => ElectricalParameters.TryGetValue("Rs", out var val) ? val : 0.0;
        set => ElectricalParameters["Rs"] = value;
    }

    public double JunctionCapacitance
    {
        get => ElectricalParameters.TryGetValue("Cj0", out var val) ? val : 0.0;
        set => ElectricalParameters["Cj0"] = value;
    }

    public double TransitTime
    {
        get => ElectricalParameters.TryGetValue("Tt", out var val) ? val : 0.0;
        set => ElectricalParameters["Tt"] = value;
    }

    public double BreakdownCurrent
    {
        get => ElectricalParameters.TryGetValue("Ibv", out var val) ? val : 1e-3;
        set => ElectricalParameters["Ibv"] = value;
    }

    public DiodeComponent()
    {
        Pins.Add(new ComponentPin("anode", "A", -20, 0));
        Pins.Add(new ComponentPin("cathode", "K", 20, 0));
        SaturationCurrent = 1e-14;
        EmissionCoefficient = 1.0;
        BreakdownVoltage = 0.0;
        SeriesResistance = 0.0;
        JunctionCapacitance = 0.0;
        TransitTime = 0.0;
        BreakdownCurrent = 1e-3;
    }

    public DiodeComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
