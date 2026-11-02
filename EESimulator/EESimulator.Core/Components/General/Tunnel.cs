using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Named Net Tunnel / Label Junction.
/// Connects nets across schematics without physical wire lines.
/// All Tunnel components with matching Label strings belong to the same electrical net.
/// </summary>
public class TunnelComponent : GeneralComponent
{
    public const string StableType = "general.tunnel";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Tunnel";
    public override string Description => "Named Net Tunnel / Label Junction";

    public override bool IsSimulatable => true;

    /// <summary>Tunnel label name. All tunnels with this exact label share the net.</summary>
    public string TunnelLabel
    {
        get => CustomProperties.TryGetValue("TunnelLabel", out var val) && val is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : (!string.IsNullOrWhiteSpace(Reference) ? Reference : Name);
        set
        {
            CustomProperties["TunnelLabel"] = value;
            Reference = value;
            NotifyModified(nameof(TunnelLabel));
        }
    }

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

    public TunnelComponent()
    {
        // Terminal pin on left
        Pins.Add(new ComponentPin("tunnel", "NET", -15, 0));
        TunnelLabel = "NET1";
    }

    public TunnelComponent(string label, double x = 0, double y = 0) : this()
    {
        Name = label;
        Reference = label;
        TunnelLabel = label;
        X = x;
        Y = y;
    }
}
