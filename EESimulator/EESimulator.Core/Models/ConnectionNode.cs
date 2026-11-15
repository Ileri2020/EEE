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
    private string _netName = "Net0";
    private double _resistance;

    public string NetName
    {
        get => _netName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A wire net name cannot be empty.", nameof(value));
            if (_netName.Equals(value, StringComparison.Ordinal))
                return;
            _netName = value;
            NotifyModified(nameof(NetName));
        }
    }

    public Guid FromComponentId { get; set; }
    public string FromPin { get; set; } = string.Empty;

    public Guid ToComponentId { get; set; }
    public string ToPin { get; set; } = string.Empty;

    public List<Point2D> RoutingPoints { get; } = new();

    public double Resistance
    {
        get => _resistance;
        set
        {
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Wire resistance must be finite and non-negative.");
            if (_resistance.Equals(value))
                return;
            _resistance = value;
            NotifyModified(nameof(Resistance));
        }
    }

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
