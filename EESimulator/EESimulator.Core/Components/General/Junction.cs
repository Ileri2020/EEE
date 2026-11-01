using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Electrical junction / node connection dot.
/// </summary>
public class JunctionComponent : GeneralComponent
{
    public const string StableType = "general.junction";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Junction";
    public override string Description => "Wire junction point";

    public JunctionComponent()
    {
        Pins.Add(new ComponentPin("node", "•", 0, 0));
    }

    public JunctionComponent(string name, double x = 0, double y = 0) : this()
    {
        Name = name;
        Reference = name;
        X = x;
        Y = y;
    }
}
