using System;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Registry;

/// <summary>
/// Factory utility for instantiating component instances from stable type IDs or definitions.
/// </summary>
public static class ComponentFactory
{
    public static ComponentNode Create(string typeIdOrName, string name = "", double x = 0, double y = 0)
    {
        return ComponentRegistry.CreateInstance(typeIdOrName, name, x, y);
    }

    public static T Create<T>(string name = "", double x = 0, double y = 0) where T : ComponentNode, new()
    {
        var comp = new T
        {
            Name = name,
            Reference = name,
            X = x,
            Y = y
        };
        return comp;
    }
}
