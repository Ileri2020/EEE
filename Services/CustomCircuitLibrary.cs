using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using EESimulator.Core.Models;
using EESimulator.Formats;
using Microsoft.Maui.Storage;

namespace EEE.Services;

/// <summary>Device-local persistence for user-created reusable circuit components.</summary>
public static class CustomCircuitLibrary
{
    private const string MetadataPrefix = "EEE_CUSTOM_CIRCUIT_V1:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string FilePath = Path.Combine(FileSystem.AppDataDirectory, "custom-circuits-v1.json");
    private static readonly object SyncRoot = new();

    public static async Task<List<CustomCircuitDefinition>> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<CustomCircuitDefinition>();
            await using var stream = File.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<List<CustomCircuitDefinition>>(stream, JsonOptions)
                   ?? new List<CustomCircuitDefinition>();
        }
        catch
        {
            return new List<CustomCircuitDefinition>();
        }
    }

    public static async Task SaveAsync(CustomCircuitDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Name)) throw new ArgumentException("A component name is required.", nameof(definition));
        ValidateSvg(definition.SvgIcon);

        List<CustomCircuitDefinition> definitions;
        lock (SyncRoot)
        {
            definitions = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<CustomCircuitDefinition>>(File.ReadAllText(FilePath), JsonOptions) ?? new List<CustomCircuitDefinition>()
                : new List<CustomCircuitDefinition>();
            definitions.RemoveAll(item => item.Id == definition.Id || item.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));
            definitions.Add(definition);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, definitions, JsonOptions);
    }

    public static string AddMetadata(string description, string componentId, IReadOnlyList<CustomCircuitPort> ports)
    {
        var payload = JsonSerializer.Serialize(new Metadata { Id = componentId, Ports = ports.ToList() }, JsonOptions);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        return $"{description ?? string.Empty}\n{MetadataPrefix}{encoded}";
    }

    public static CustomCircuitDefinition? TryReadRemoteDefinition(CircuitExampleItem example)
    {
        try
        {
            var circuit = EeePlatformSerializer.LoadFromEeeJson(example.RawEeeJson);
            var line = circuit.Description.Split('\n').LastOrDefault(value => value.StartsWith(MetadataPrefix, StringComparison.Ordinal));
            if (line == null) return null;
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(line[MetadataPrefix.Length..]));
            var metadata = JsonSerializer.Deserialize<Metadata>(json, JsonOptions);
            if (metadata?.Ports == null || metadata.Ports.Count == 0) return null;
            return new CustomCircuitDefinition
            {
                Id = string.IsNullOrWhiteSpace(metadata.Id) ? example.Id : metadata.Id,
                Name = example.Title,
                SvgIcon = example.Icon,
                CircuitJson = example.RawEeeJson,
                Ports = metadata.Ports
            };
        }
        catch
        {
            return null;
        }
    }

    private static void ValidateSvg(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg)) return;
        if (svg.Length > 20_000)
            throw new ArgumentException("Provide a small inline SVG without scripts, event handlers, or external resources.", nameof(svg));

        try
        {
            var document = XDocument.Parse(svg, LoadOptions.None);
            var allowedElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "title" };
            var allowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "xmlns", "viewBox", "width", "height", "x", "y", "x1", "x2", "y1", "y2", "cx", "cy", "r", "rx", "ry",
                "d", "points", "fill", "fill-rule", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "opacity", "transform", "font-size", "text-anchor"
            };
            if (!string.Equals(document.Root?.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase) ||
                document.Root.DescendantsAndSelf().Any(element => !allowedElements.Contains(element.Name.LocalName)) ||
                document.Root.DescendantsAndSelf().Attributes().Any(attribute => !allowedAttributes.Contains(attribute.Name.LocalName) ||
                    attribute.Value.Contains("url(", StringComparison.OrdinalIgnoreCase) ||
                    attribute.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                    attribute.Value.Contains("http://", StringComparison.OrdinalIgnoreCase) && !attribute.Value.Equals("http://www.w3.org/2000/svg", StringComparison.OrdinalIgnoreCase) ||
                    attribute.Value.Contains("https://", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("The SVG uses unsupported or external content. Use basic SVG shapes and paths only.", nameof(svg));
        }
        catch (System.Xml.XmlException ex)
        {
            throw new ArgumentException("The SVG icon is not valid XML.", nameof(svg), ex);
        }
    }

    private sealed class Metadata
    {
        public string Id { get; set; } = string.Empty;
        public List<CustomCircuitPort> Ports { get; set; } = new();
    }
}
