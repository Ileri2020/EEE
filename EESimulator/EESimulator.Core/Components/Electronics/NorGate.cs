using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// 2-Input NOR digital logic gate component.
/// Output is HIGH only when all inputs are LOW.
/// </summary>
public class NorGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.nor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "NOR";
    public override string Description => "2-Input NOR Gate";

    public NorGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -24, -10));
        Pins.Add(new ComponentPin("in2", "B", -24, 10));
        Pins.Add(new ComponentPin("out", "Y", 24, 0));
    }

    public NorGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
