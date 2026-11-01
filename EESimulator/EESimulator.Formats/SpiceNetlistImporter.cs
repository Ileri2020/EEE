using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using EESimulator.Core.Components;
using EESimulator.Core.Models;

namespace EESimulator.Formats;

/// <summary>
/// Parser and importer for SPICE netlists (.cir / .sp).
/// Reads standard SPICE lines (R, C, L, V, I) and converts them into an EESimulator CircuitNode.
/// </summary>
public class SpiceNetlistImporter
{
    public static CircuitNode ImportFromSpice(string netlistText, string circuitTitle = "Imported Circuit")
    {
        var circuit = new CircuitNode
        {
            Title = circuitTitle
        };

        if (string.IsNullOrWhiteSpace(netlistText))
            return circuit;

        var lines = netlistText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int componentIndex = 0;
        double currentX = 100;
        double currentY = 100;

        var nodePinMap = new Dictionary<string, List<(ComponentNode Comp, string Pin)>>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("*") || line.StartsWith(".") || string.IsNullOrWhiteSpace(line))
                continue; // Comment or SPICE directive

            var parts = Regex.Split(line, @"\s+");
            if (parts.Length < 4) continue;

            string name = parts[0];
            string node1 = parts[1];
            string node2 = parts[2];
            char typeChar = char.ToUpperInvariant(name[0]);

            double val = ParseSpiceValue(parts[3]);

            ComponentNode? comp = typeChar switch
            {
                'R' => ComponentRegistry.CreateInstance("Resistor", name, currentX, currentY),
                'C' => ComponentRegistry.CreateInstance("Capacitor", name, currentX, currentY),
                'L' => ComponentRegistry.CreateInstance("Inductor", name, currentX, currentY),
                'V' => ComponentRegistry.CreateInstance("VoltageSource", name, currentX, currentY),
                _ => null
            };

            if (comp != null)
            {
                if (comp is ResistorNode r) r.Resistance = val;
                else if (comp is CapacitorNode c) c.Capacitance = val;
                else if (comp is InductorNode l) l.Inductance = val;
                else if (comp is VoltageSourceNode v) v.Voltage = val;

                circuit.AddChild(comp);

                // Register pins to nodes for connection wiring
                RegisterNodePin(nodePinMap, node1, comp, comp.Pins[0].Id);
                RegisterNodePin(nodePinMap, node2, comp, comp.Pins.Count > 1 ? comp.Pins[1].Id : comp.Pins[0].Id);

                componentIndex++;
                currentX += 120;
                if (componentIndex % 4 == 0)
                {
                    currentX = 100;
                    currentY += 120;
                }
            }
        }

        // Build connections for pins sharing the same net
        foreach (var kvp in nodePinMap)
        {
            string netName = kvp.Key;
            var pins = kvp.Value;
            for (int i = 0; i < pins.Count - 1; i++)
            {
                var from = pins[i];
                var to = pins[i + 1];
                var conn = new ConnectionNode(from.Comp.Id, from.Pin, to.Comp.Id, to.Pin, netName);
                circuit.AddChild(conn);
            }
        }

        return circuit;
    }

    private static void RegisterNodePin(Dictionary<string, List<(ComponentNode, string)>> map, string node, ComponentNode comp, string pin)
    {
        if (!map.TryGetValue(node, out var list))
        {
            list = new List<(ComponentNode, string)>();
            map[node] = list;
        }
        list.Add((comp, pin));
    }

    private static double ParseSpiceValue(string str)
    {
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            return val;

        // Handle SPICE multipliers (k, m, u, n, p, meg)
        str = str.Trim();
        double mult = 1.0;
        if (str.EndsWith("k", StringComparison.OrdinalIgnoreCase)) { mult = 1e3; str = str[..^1]; }
        else if (str.EndsWith("meg", StringComparison.OrdinalIgnoreCase)) { mult = 1e6; str = str[..^3]; }
        else if (str.EndsWith("m", StringComparison.OrdinalIgnoreCase)) { mult = 1e-3; str = str[..^1]; }
        else if (str.EndsWith("u", StringComparison.OrdinalIgnoreCase)) { mult = 1e-6; str = str[..^1]; }
        else if (str.EndsWith("n", StringComparison.OrdinalIgnoreCase)) { mult = 1e-9; str = str[..^1]; }
        else if (str.EndsWith("p", StringComparison.OrdinalIgnoreCase)) { mult = 1e-12; str = str[..^1]; }

        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
            return val * mult;

        return 0.0;
    }
}
