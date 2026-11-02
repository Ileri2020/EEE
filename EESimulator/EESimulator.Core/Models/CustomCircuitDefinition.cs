using System;
using System.Collections.Generic;

namespace EESimulator.Core.Models;

/// <summary>One externally exposed pin in a saved custom-circuit component.</summary>
public sealed class CustomCircuitPort
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid InnerComponentId { get; set; }
    public string InnerPinId { get; set; } = string.Empty;
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
}

/// <summary>Locally persisted reusable circuit definition and its exposed ports.</summary>
public sealed class CustomCircuitDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string SvgIcon { get; set; } = string.Empty;
    public string CircuitJson { get; set; } = string.Empty;
    public List<CustomCircuitPort> Ports { get; set; } = new();
}
