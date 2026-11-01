using System;
using System.Collections.Generic;
using EESimulator.Core.Models;

namespace EESimulator.Core.Electrical;

public class PinReference
{
    public Guid ComponentId { get; set; }
    public string PinId { get; set; } = string.Empty;

    public PinReference() { }

    public PinReference(Guid componentId, string pinId)
    {
        ComponentId = componentId;
        PinId = pinId;
    }

    public override bool Equals(object? obj) =>
        obj is PinReference other && ComponentId == other.ComponentId && PinId == other.PinId;

    public override int GetHashCode() => HashCode.Combine(ComponentId, PinId);
}

/// <summary>
/// Represents an electrical net (equipotential node in circuit theory)
/// connecting multiple component pins and wires.
/// </summary>
public class NetNode : RootNode
{
    public string NetName { get; set; } = string.Empty;
    public bool IsGround { get; set; } = false;

    public HashSet<PinReference> ConnectedPins { get; } = new();

    public NetNode()
    {
        Name = "Net";
    }

    public NetNode(string name, bool isGround = false)
    {
        Name = name;
        NetName = name;
        IsGround = isGround;
    }
}
