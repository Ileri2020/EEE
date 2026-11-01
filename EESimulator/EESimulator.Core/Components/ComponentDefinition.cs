using System;
using System.Collections.Generic;

namespace EESimulator.Core.Components;

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
    public string TypeName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string ReferencePrefix { get; set; } = "U";
    public Type NodeType { get; set; } = typeof(object);
    public List<ComponentPinDefinition> Pins { get; } = new();
    public Dictionary<string, double> DefaultParameters { get; } = new();
    public Func<object> FactoryMethod { get; set; } = () => null!;
}
