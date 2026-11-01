using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Magnetic coupling between two inductors (M = k * sqrt(L1 * L2)).
/// Fully mapped to SpiceSharp.Components.MutualInductance.
/// </summary>
public class MutualInductanceComponent : ElectricalComponent
{
    public const string StableType = "electrical.mutualinductance";

    public override string StableTypeId => StableType;
    public override string ComponentType => "MutualInductance";
    public override string SpicePrefix => "K";
    public override string Description => "Magnetic coupling between two inductors (K-factor: 0.0 to 1.0)";

    public string Inductor1
    {
        get => CustomProperties.TryGetValue("Inductor1", out var v) ? v?.ToString() ?? "L1" : "L1";
        set => CustomProperties["Inductor1"] = value;
    }

    public string Inductor2
    {
        get => CustomProperties.TryGetValue("Inductor2", out var v) ? v?.ToString() ?? "L2" : "L2";
        set => CustomProperties["Inductor2"] = value;
    }

    public double Coupling
    {
        get => ElectricalParameters.TryGetValue("Coupling", out var val) ? val : 0.99;
        set => ElectricalParameters["Coupling"] = Math.Clamp(value, 0.0, 1.0);
    }

    public MutualInductanceComponent()
    {
        // 4 terminal pins for primary and secondary coupled coils
        Pins.Add(new ComponentPin("pri1", "P1", -20, -10));
        Pins.Add(new ComponentPin("pri2", "P2", -20, 10));
        Pins.Add(new ComponentPin("sec1", "S1", 20, -10));
        Pins.Add(new ComponentPin("sec2", "S2", 20, 10));

        Inductor1 = "L1";
        Inductor2 = "L2";
        Coupling = 0.99;
    }

    public MutualInductanceComponent(string name, string ind1 = "L1", string ind2 = "L2", double coupling = 0.99) : this()
    {
        Name = name;
        Reference = name;
        Inductor1 = ind1;
        Inductor2 = ind2;
        Coupling = coupling;
    }
}
