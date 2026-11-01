using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Linear electrical inductor with initial current condition (IC) and flux transient integration.
/// </summary>
public class InductorComponent : ElectricalComponent
{
    public const string StableType = "electrical.inductor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Inductor";
    public override string SpicePrefix => "L";
    public override string Description => "Linear two-terminal electrical inductor with IC and transient support";

    public double Inductance
    {
        get => ElectricalParameters.TryGetValue("Inductance", out var val) ? val : 1e-3;
        set => ElectricalParameters["Inductance"] = value;
    }

    public double InitialCondition
    {
        get => ElectricalParameters.TryGetValue("IC", out var val) ? val : 0.0;
        set => ElectricalParameters["IC"] = value;
    }

    public double SeriesMultiplier
    {
        get => ElectricalParameters.TryGetValue("SeriesMultiplier", out var val) ? val : 1.0;
        set => ElectricalParameters["SeriesMultiplier"] = value;
    }

    public double ParallelMultiplier
    {
        get => ElectricalParameters.TryGetValue("ParallelMultiplier", out var val) ? val : 1.0;
        set => ElectricalParameters["ParallelMultiplier"] = value;
    }

    public InductorComponent()
    {
        Pins.Add(new ComponentPin("pin1", "1", -20, 0));
        Pins.Add(new ComponentPin("pin2", "2", 20, 0));
        Inductance = 1e-3;
        InitialCondition = 0.0;
        SeriesMultiplier = 1.0;
        ParallelMultiplier = 1.0;
    }

    public InductorComponent(string name, double inductance = 1e-3) : this()
    {
        Name = name;
        Reference = name;
        Inductance = inductance;
    }
}
