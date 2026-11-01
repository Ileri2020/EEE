using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// 2-Input XOR (Exclusive-OR) digital logic gate component.
/// Output is HIGH when inputs are different.
/// </summary>
public class XorGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.xor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "XOR";
    public override string Description => "2-Input XOR Gate";

    public XorGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -24, -10));
        Pins.Add(new ComponentPin("in2", "B", -24, 10));
        Pins.Add(new ComponentPin("out", "Y", 24, 0));
    }

    public XorGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
