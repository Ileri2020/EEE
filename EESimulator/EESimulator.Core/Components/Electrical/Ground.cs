using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Circuit ground reference (Node 0).
/// </summary>
public class GroundComponent : ElectricalComponent
{
    public const string StableType = "electrical.ground";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Ground";
    public override string SpicePrefix => "GND";
    public override string Description => "Circuit ground reference (Node 0)";

    public GroundComponent()
    {
        Pins.Add(new ComponentPin("gnd", "GND", 0, -10));
    }

    public GroundComponent(string name) : this()
    {
        Name = name;
        Reference = name;
    }
}
