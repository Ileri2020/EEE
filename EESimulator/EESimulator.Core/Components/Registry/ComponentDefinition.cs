using System;
using System.Collections.Generic;

namespace EESimulator.Core.Components.Registry;

public class ComponentPinDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double DefaultRelativeX { get; set; }
    public double DefaultRelativeY { get; set; }

    public ComponentPinDefinition() { }

    public ComponentPinDefinition(string id, string name, double relX, double relY)
    {
        Id = id;
        Name = name;
        DefaultRelativeX = relX;
        DefaultRelativeY = relY;
    }
}

public class ComponentDefinition
{
    public string StableTypeId { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string ReferencePrefix { get; set; } = "U";
    public string SymbolKey { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsSimulatable { get; set; } = true;
    public Type NodeType { get; set; } = typeof(object);
    public List<ComponentPinDefinition> Pins { get; } = new();
    public Dictionary<string, double> DefaultParameters { get; } = new();
    public Func<object> FactoryMethod { get; set; } = () => null!;
}
