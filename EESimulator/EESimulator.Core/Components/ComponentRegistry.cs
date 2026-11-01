using System;
using System.Collections.Generic;
using EESimulator.Core.Models;
using RegistryImpl = EESimulator.Core.Components.Registry.ComponentRegistry;
using RegDef = EESimulator.Core.Components.Registry.ComponentDefinition;

namespace EESimulator.Core.Components;

/// <summary>
/// Forwarding bridge for backwards compatibility with legacy namespace imports.
/// </summary>
public static class ComponentRegistry
{
    public static void Register<T>(string typeName, string category, string referencePrefix, Action<ComponentDefinition>? configure = null) where T : ComponentNode, new()
    {
        RegistryImpl.Register<T>(typeName.ToLowerInvariant(), typeName, category, referencePrefix);
    }

    public static bool TryGetDefinition(string typeName, out ComponentDefinition definition)
    {
        if (RegistryImpl.TryGetDefinition(typeName, out var regDef))
        {
            definition = Map(regDef);
            return true;
        }
        definition = null!;
        return false;
    }

    public static ComponentDefinition GetDefinition(string typeName)
    {
        return Map(RegistryImpl.GetDefinition(typeName));
    }

    public static IEnumerable<ComponentDefinition> GetAllDefinitions()
    {
        foreach (var def in RegistryImpl.GetAllDefinitions())
            yield return Map(def);
    }

    public static ComponentNode CreateInstance(string typeName, string name = "", double x = 0, double y = 0)
    {
        return RegistryImpl.CreateInstance(typeName, name, x, y);
    }

    private static ComponentDefinition Map(RegDef def)
    {
        var cd = new ComponentDefinition
        {
            TypeName = def.TypeName,
            DisplayName = def.DisplayName,
            Category = def.Category,
            Description = def.Description,
            ReferencePrefix = def.ReferencePrefix,
            NodeType = def.NodeType,
            FactoryMethod = def.FactoryMethod
        };
        foreach (var p in def.Pins)
            cd.Pins.Add(new ComponentPinDefinition(p.Id, p.Name, p.DefaultRelativeX, p.DefaultRelativeY));
        foreach (var kvp in def.DefaultParameters)
            cd.DefaultParameters[kvp.Key] = kvp.Value;
        return cd;
    }
}
