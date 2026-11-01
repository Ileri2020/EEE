using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// 2-Input OR digital logic gate component.
/// Output is HIGH when at least one input is HIGH.
/// </summary>
public class OrGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.or";

    public override string StableTypeId => StableType;
    public override string ComponentType => "OR";
    public override string Description => "2-Input OR Gate";

    public OrGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -24, -10));
        Pins.Add(new ComponentPin("in2", "B", -24, 10));
        Pins.Add(new ComponentPin("out", "Y", 24, 0));
    }

    public OrGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
