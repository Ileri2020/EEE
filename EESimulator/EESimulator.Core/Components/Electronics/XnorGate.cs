using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// 2-Input XNOR (Exclusive-NOR) digital logic gate component.
/// Output is HIGH when inputs are equal.
/// </summary>
public class XnorGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.xnor";

    public override string StableTypeId => StableType;
    public override string ComponentType => "XNOR";
    public override string Description => "2-Input XNOR Gate";

    public XnorGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -24, -10));
        Pins.Add(new ComponentPin("in2", "B", -24, 10));
        Pins.Add(new ComponentPin("out", "Y", 24, 0));
    }

    public XnorGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
