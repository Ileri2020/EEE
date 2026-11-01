using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// NOT digital logic gate (Inverter) component.
/// Output is the logical inversion of the input.
/// </summary>
public class NotGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.not";

    public override string StableTypeId => StableType;
    public override string ComponentType => "NOT";
    public override string Description => "Inverter / NOT Gate";

    public NotGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -24, 0));
        Pins.Add(new ComponentPin("out", "Y", 24, 0));
    }

    public NotGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
