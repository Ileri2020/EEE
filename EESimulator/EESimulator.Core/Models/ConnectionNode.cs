using System;
using System.Collections.Generic;

namespace EESimulator.Core.Models;

/// <summary>
/// Point in schematic coordinates.
/// </summary>
public struct Point2D
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point2D(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Node representing an electrical wire or net connection between component terminals.
/// </summary>
public class ConnectionNode : RootNode
{
    public string NetName { get; set; } = "Net0";

    public Guid FromComponentId { get; set; }
    public string FromPin { get; set; } = string.Empty;

    public Guid ToComponentId { get; set; }
    public string ToPin { get; set; } = string.Empty;

    public List<Point2D> RoutingPoints { get; } = new();

    public double Resistance { get; set; } = 0.0;

    public ConnectionNode()
    {
        Name = "Wire";
    }

    public ConnectionNode(Guid fromComponent, string fromPin, Guid toComponent, string toPin, string netName = "")
    {
        FromComponentId = fromComponent;
        FromPin = fromPin;
        ToComponentId = toComponent;
        ToPin = toPin;
        NetName = string.IsNullOrEmpty(netName) ? $"Net_{Guid.NewGuid().ToString("N")[..6]}" : netName;
        Name = $"Wire_{FromPin}_to_{ToPin}";
    }
}
