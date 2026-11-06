using System;
using System.IO;
using System.Text.Json;
using EESimulator.Core.Models;

namespace EESimulator.Formats;

/// <summary>
/// Serializes and deserializes native EESimulator project files (.eesim).
/// Preserves complete RootNode object hierarchies, positions, connection graphs, and metadata.
/// </summary>
public class EesimSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeToJson(CircuitNode circuit)
    {
        return JsonSerializer.Serialize(circuit, Options);
    }

    public static void SaveToFile(CircuitNode circuit, string filePath)
    {
        string json = SerializeToJson(circuit);
        File.WriteAllText(filePath, json);
    }

    public static CircuitNode? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Project file not found: {filePath}");

        return LoadFromJson(File.ReadAllText(filePath));
    }

    public static CircuitNode? LoadFromJson(string json)
        => JsonSerializer.Deserialize<CircuitNode>(json, Options);
}
