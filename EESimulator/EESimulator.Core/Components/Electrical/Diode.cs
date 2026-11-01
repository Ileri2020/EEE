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

    public DiodeComponent()
    {
        Pins.Add(new ComponentPin("anode", "A", -20, 0));
        Pins.Add(new ComponentPin("cathode", "K", 20, 0));
        SaturationCurrent = 1e-14;
    }

    public DiodeComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
