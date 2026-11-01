using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// 2-Input NAND digital logic gate component.
/// Output is LOW only when all inputs are HIGH.
/// </summary>
public class NandGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.nand";

    public override string StableTypeId => StableType;
    public override string ComponentType => "NAND";
    public override string Description => "2-Input NAND Gate";

    public NandGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -24, -10));
        Pins.Add(new ComponentPin("in2", "B", -24, 10));
        Pins.Add(new ComponentPin("out", "Y", 24, 0));
    }

    public NandGateComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
