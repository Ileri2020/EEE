using System;
using System.IO;
using System.Text;
using EESimulator.Core.Models;

namespace EESimulator.Formats;

/// <summary>
/// Exporter for KiCad S-Expression schematic format (.kicad_sch),
/// the modern open-source standard for visual electrical schematic exchange.
/// </summary>
public class KiCadExporter
{
    public static string ExportToKiCadSch(CircuitNode circuit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("(kicad_sch (version 20230121) (generator \"EESimulator\")");
        sb.AppendLine($"  (uuid \"{circuit.Id}\")");
        sb.AppendLine("  (paper \"A4\")");
        sb.AppendLine($"  (title_block (title \"{circuit.Title}\"))");

        foreach (var comp in circuit.Components)
        {
            sb.AppendLine($"  (symbol (lib_id \"Device:{comp.ComponentType}\") (at {comp.X} {comp.Y} {comp.Rotation}) (unit 1)");
            sb.AppendLine($"    (uuid \"{comp.Id}\")");
            sb.AppendLine($"    (property \"Reference\" \"{comp.Reference}\" (at {comp.X} {comp.Y - 10} 0))");
            sb.AppendLine($"    (property \"Value\" \"{comp.Name}\" (at {comp.X} {comp.Y + 10} 0))");
            sb.AppendLine("  )");
        }

        sb.AppendLine(")");
        return sb.ToString();
    }

    public static void SaveToFile(CircuitNode circuit, string filePath)
    {
        string sch = ExportToKiCadSch(circuit);
        File.WriteAllText(filePath, sch);
    }
}
