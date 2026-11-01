using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Net label for named node connectivity (e.g. 'VCC', 'CLK', 'RESET').
/// </summary>
public class LabelComponent : GeneralComponent
{
    public const string StableType = "general.label";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Label";
    public override string Description => "Net connection label";

    public string NetName { get; set; } = "NET1";

    public LabelComponent()
    {
        Pins.Add(new ComponentPin("pin", "1", 0, 0));
    }

    public LabelComponent(string name, string netName) : this()
    {
        Name = name;
        Reference = name;
        NetName = netName;
    }
}
