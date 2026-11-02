using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Do Not Connect (DNC / No Connect) marker.
/// Explicitly terminates unused IC pins, bus bits, or schematic nodes.
/// Prevents unrouted pin warnings and floating-node singularity errors in simulation.
/// </summary>
public class DoNotConnectComponent : GeneralComponent
{
    public const string StableType = "general.dnc";

    public override string StableTypeId => StableType;
    public override string ComponentType => "DoNotConnect";
    public override string Description => "Do Not Connect (DNC) Marker";

    public override bool IsSimulatable => true;

    /// <summary>Logisim Bit Width (1 to 32 bits, default 1).</summary>
    public int BitWidth
    {
        get => ElectricalParameters.TryGetValue("BitWidth", out var val) && val >= 1 ? (int)val : 1;
        set
        {
            ElectricalParameters["BitWidth"] = Math.Clamp(value, 1, 32);
            NotifyModified(nameof(BitWidth));
        }
    }

    public DoNotConnectComponent()
    {
        Pins.Add(new ComponentPin("pin", "NC", 0, 0));
    }

    public DoNotConnectComponent(string name, double x = 0, double y = 0) : this()
    {
        Name = name;
        Reference = name;
        X = x;
        Y = y;
    }
}
