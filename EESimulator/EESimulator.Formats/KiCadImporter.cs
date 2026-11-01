using System;
using System.Text.RegularExpressions;
using EESimulator.Core.Components;
using EESimulator.Core.Models;

namespace EESimulator.Formats;

/// <summary>
/// Parser and importer for KiCad S-expression schematic files (.kicad_sch).
/// Reads symbol definitions, references, coordinates, and builds a CircuitNode.
/// </summary>
public class KiCadImporter
{
    public static CircuitNode ImportFromKiCadSch(string kicadSchText, string defaultTitle = "KiCad Imported Circuit")
    {
        var circuit = new CircuitNode
        {
            Title = defaultTitle
        };

        if (string.IsNullOrWhiteSpace(kicadSchText))
            return circuit;

        // Parse title if present
        var titleMatch = Regex.Match(kicadSchText, @"\(title\s+""([^""]+)""\)");
        if (titleMatch.Success)
            circuit.Title = titleMatch.Groups[1].Value;

        // Parse symbols: (symbol (lib_id "Device:Resistor") (at 100 200 0) ... (property "Reference" "R1") ... )
        var symbolMatches = Regex.Matches(kicadSchText, @"\(symbol\s+\(lib_id\s+""(?:Device:)?([^""]+)""\)\s+\(at\s+([\d\.-]+)\s+([\d\.-]+)(?:\s+([\d\.-]+))?\).*?\(property\s+""Reference""\s+""([^""]+)""\)", RegexOptions.Singleline);

        foreach (Match m in symbolMatches)
        {
            string rawType = m.Groups[1].Value;
            double x = double.TryParse(m.Groups[2].Value, out var px) ? px : 100;
            double y = double.TryParse(m.Groups[3].Value, out var py) ? py : 100;
            string reference = m.Groups[5].Value;

            string normalizedType = rawType.ToLowerInvariant() switch
            {
                var t when t.Contains("resistor") || t == "r" => "Resistor",
                var t when t.Contains("capacitor") || t == "c" => "Capacitor",
                var t when t.Contains("inductor") || t == "l" => "Inductor",
                var t when t.Contains("vsource") || t.Contains("voltage") => "VoltageSource",
                var t when t.Contains("gnd") || t.Contains("ground") => "Ground",
                _ => "Resistor"
            };

            var comp = ComponentRegistry.CreateInstance(normalizedType, reference, x, y);
            circuit.AddChild(comp);
        }

        return circuit;
    }
}
