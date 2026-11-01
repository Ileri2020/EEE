using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum LogicGateType
{
    AND,
    OR,
    NOT,
    NAND,
    NOR,
    XOR
}

/// <summary>
/// Digital logic gate component.
/// </summary>
public class LogicGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.logicgate";

    public override string StableTypeId => StableType;
    public override string ComponentType => "LogicGate";
    public override string Description => "Digital Logic Gate";

    public LogicGateType GateType { get; set; } = LogicGateType.AND;

    public LogicGateComponent()
    {
        Pins.Add(new ComponentPin("in1", "A", -20, -10));
        Pins.Add(new ComponentPin("in2", "B", -20, 10));
        Pins.Add(new ComponentPin("out", "Y", 20, 0));
    }

    public LogicGateComponent(string name, LogicGateType gateType = LogicGateType.AND) : this()
    {
        Name = name;
        Reference = name;
        GateType = gateType;
        if (gateType == LogicGateType.NOT)
        {
            Pins.RemoveAll(p => p.Id == "in2");
            var in1 = Pins.Find(p => p.Id == "in1");
            if (in1 != null) in1.RelativeY = 0;
        }
    }
}
