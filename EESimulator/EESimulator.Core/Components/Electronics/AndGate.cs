using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// 2-Input AND digital logic gate component.
/// Output is HIGH only when all inputs are HIGH.
/// </summary>
public class AndGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.and";

    public override string StableTypeId => StableType;
    public override string ComponentType => "AND";
    public override string Description => "2-Input AND Gate";

    public AndGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -24, -10));
        Pins.Add(new ComponentPin("in2", "B", -24, 10));
        Pins.Add(new ComponentPin("out", "Y", 24, 0));
    }

    public AndGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
