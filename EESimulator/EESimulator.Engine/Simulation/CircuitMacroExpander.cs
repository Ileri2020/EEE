using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.General;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using EESimulator.Formats;

namespace EESimulator.Engine.Simulation;

internal sealed class CircuitMacroExpansion
{
    public CircuitNode Circuit { get; init; } = new();
    public bool HasMacros { get; init; }
    public List<MacroRuntimeBinding> Bindings { get; init; } = new();
}

internal sealed class MacroRuntimeBinding
{
    public CustomCircuitComponent OriginalMacro { get; init; } = null!;
    public List<PinReference> PortPins { get; } = new();
    public HashSet<Guid> InnerComponentIds { get; } = new();
}

/// <summary>Flattens saved circuit components into the parent SpiceSharp netlist.</summary>
internal static class CircuitMacroExpander
{
    public static CircuitMacroExpansion Expand(CircuitNode source)
    {
        var sourceMacros = source.Components.OfType<CustomCircuitComponent>().ToDictionary(component => component.Id);
        if (sourceMacros.Count == 0)
            return new CircuitMacroExpansion { Circuit = source, HasMacros = false };

        var expanded = EeePlatformSerializer.LoadFromEeeJson(EeePlatformSerializer.ExportToEee(source));
        var macros = expanded.Components.OfType<CustomCircuitComponent>().ToList();
        var endpointMap = new Dictionary<(Guid ComponentId, string PinId), PinReference>();
        var topConnections = expanded.Connections.ToList();
        foreach (var connection in topConnections)
            expanded.RemoveChild(connection);

        var bindings = new List<MacroRuntimeBinding>();
        foreach (var macro in macros)
        {
            if (string.IsNullOrWhiteSpace(macro.DefinitionJson))
                throw new InvalidOperationException($"Custom component '{macro.Reference}' has no saved circuit definition.");
            var inner = EeePlatformSerializer.LoadFromEeeJson(macro.DefinitionJson);
            if (inner.Components.Any(component => component is CustomCircuitComponent))
                throw new InvalidOperationException("Nested custom circuit components are not supported yet.");

            var idMap = inner.Components.ToDictionary(component => component.Id, _ => Guid.NewGuid());
            var runtimeBinding = new MacroRuntimeBinding { OriginalMacro = sourceMacros[macro.Id] };
            foreach (var original in inner.Components)
            {
                var clone = original.Clone();
                clone.Id = idMap[original.Id];
                clone.Name = MakeUniqueName(macro.Name, original.Name);
                clone.Reference = clone.Name;
                expanded.AddChild(clone);
                runtimeBinding.InnerComponentIds.Add(clone.Id);
            }

            foreach (var port in macro.Ports)
            {
                if (!idMap.TryGetValue(port.InnerComponentId, out var mappedComponentId))
                    throw new InvalidOperationException($"Port '{port.Name}' in '{macro.Reference}' references a missing internal component.");
                var mappedPin = new PinReference(mappedComponentId, port.InnerPinId);
                runtimeBinding.PortPins.Add(mappedPin);
                endpointMap[(macro.Id, port.Id)] = mappedPin;
            }

            foreach (var connection in inner.Connections)
            {
                var clone = new ConnectionNode(idMap[connection.FromComponentId], connection.FromPin,
                    idMap[connection.ToComponentId], connection.ToPin, connection.NetName)
                {
                    Id = Guid.NewGuid(),
                    Resistance = connection.Resistance
                };
                foreach (var point in connection.RoutingPoints)
                    clone.RoutingPoints.Add(new Point2D(point.X, point.Y));
                expanded.AddChild(clone);
            }

            expanded.RemoveChild(macro);
            bindings.Add(runtimeBinding);
        }

        foreach (var connection in topConnections)
        {
            var from = endpointMap.TryGetValue((connection.FromComponentId, connection.FromPin), out var mappedFrom)
                ? mappedFrom
                : new PinReference(connection.FromComponentId, connection.FromPin);
            var to = endpointMap.TryGetValue((connection.ToComponentId, connection.ToPin), out var mappedTo)
                ? mappedTo
                : new PinReference(connection.ToComponentId, connection.ToPin);
            if (from.ComponentId == to.ComponentId && from.PinId == to.PinId) continue;
            expanded.AddChild(new ConnectionNode(from.ComponentId, from.PinId, to.ComponentId, to.PinId, connection.NetName)
            {
                Id = Guid.NewGuid(),
                Resistance = connection.Resistance
            });
        }

        return new CircuitMacroExpansion { Circuit = expanded, HasMacros = true, Bindings = bindings };
    }

    public static void CopyMeasurementsBack(CircuitNode original, CircuitMacroExpansion expansion, SimulationResultsData results)
    {
        if (!expansion.HasMacros) return;
        foreach (var component in original.Components.Where(component => component is not CustomCircuitComponent))
        {
            var flattened = expansion.Circuit.Components.FirstOrDefault(candidate => candidate.Id == component.Id);
            if (flattened == null) continue;
            component.LiveVoltage = flattened.LiveVoltage;
            component.LiveCurrent = flattened.LiveCurrent;
            if (component is EESimulator.Core.Components.Electrical.BulbComponent originalBulb &&
                flattened is EESimulator.Core.Components.Electrical.BulbComponent flatBulb)
            {
                originalBulb.Brightness = flatBulb.Brightness;
                originalBulb.IsOn = flatBulb.IsOn;
            }
        }

        var topology = NetlistBuilder.BuildTopology(expansion.Circuit);
        foreach (var binding in expansion.Bindings)
        {
            var portNodes = binding.PortPins.Select(pin => ReadNodeVoltage(topology, pin, results)).ToList();
            binding.OriginalMacro.LiveVoltage = portNodes.Count > 1 ? portNodes[0] - portNodes[1] : portNodes.FirstOrDefault();
            if (binding.PortPins.Count > 0 && topology.PinToNetMap.TryGetValue(binding.PortPins[0], out var portNet))
            {
                double current = 0;
                foreach (var component in expansion.Circuit.Components.Where(component => binding.InnerComponentIds.Contains(component.Id) && component.Pins.Count >= 2))
                {
                    var pin0 = new PinReference(component.Id, component.Pins[0].Id);
                    var pin1 = new PinReference(component.Id, component.Pins[1].Id);
                    double branchCurrent = results.OperatingPointCurrents.GetValueOrDefault($"I({component.Name})", component.LiveCurrent);
                    if (topology.PinToNetMap.TryGetValue(pin0, out var net0) && net0.NetName == portNet.NetName) current += branchCurrent;
                    if (topology.PinToNetMap.TryGetValue(pin1, out var net1) && net1.NetName == portNet.NetName) current -= branchCurrent;
                }
                binding.OriginalMacro.LiveCurrent = current;
            }
            results.OperatingPointVoltages[$"V({binding.OriginalMacro.Name})"] = binding.OriginalMacro.LiveVoltage;
            results.OperatingPointCurrents[$"I({binding.OriginalMacro.Name})"] = binding.OriginalMacro.LiveCurrent;
        }
    }

    private static double ReadNodeVoltage(CircuitTopology topology, PinReference pin, SimulationResultsData results)
    {
        if (!topology.PinToNetMap.TryGetValue(pin, out var net) || net.IsGround) return 0;
        return results.OperatingPointVoltages.GetValueOrDefault(net.NetName);
    }

    private static string MakeUniqueName(string macroName, string innerName)
    {
        var cleanPrefix = new string(macroName.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        var cleanName = new string(innerName.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return $"{cleanPrefix}_{cleanName}";
    }
}
