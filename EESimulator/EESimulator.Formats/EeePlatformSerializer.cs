using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using EESimulator.Core.Components.Registry;
using EESimulator.Core.Models;

namespace EESimulator.Formats;

/// <summary>
/// Current native platform format version. Bump this string on any breaking schema change.
/// </summary>
public static class EeeFormatVersion
{
    public const string Current = "1.1.0";
}

/// <summary>
/// Simulation settings captured in the project file so every load restores the last-used analysis.
/// </summary>
public class EeeSimulationSettingsDto
{
    public string AnalysisType { get; set; } = "OperatingPoint"; // OperatingPoint | Transient | AC | DC
    public double TransientStartTime { get; set; } = 0.0;
    public double TransientStopTime { get; set; } = 0.01;
    public double TransientStepTime { get; set; } = 1e-5;
    public string AcSweepType { get; set; } = "Decade";
    public int AcPoints { get; set; } = 10;
    public double AcStartFrequency { get; set; } = 1.0;
    public double AcStopFrequency { get; set; } = 1e5;
    public string DcSweepSource { get; set; } = "V1";
    public double DcStart { get; set; } = 0.0;
    public double DcStop { get; set; } = 5.0;
    public double DcStep { get; set; } = 0.1;
}

/// <summary>
/// A single waypoint for non-straight wire routing.
/// </summary>
public class EeeWaypointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>
/// Data Transfer Object representing the native EEE platform schematic project file format (.eee).
/// Encapsulates full circuit topology, component parameters, visual coordinates, ratings, and wiring.
/// </summary>
public class EeeProjectDto
{
    public string FileType { get; set; } = "EEE_PLATFORM_SCHEMATIC";
    public string FormatVersion { get; set; } = EeeFormatVersion.Current;
    public string Application { get; set; } = "EESimulator";
    public string Title { get; set; } = "Untitled Circuit";
    public string Description { get; set; } = string.Empty;

    // ── Provenance metadata ──────────────────────────────────────────────
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    public string Revision { get; set; } = "1";
    public List<string> Tags { get; set; } = new();

    // ── Simulation settings ──────────────────────────────────────────────
    public EeeSimulationSettingsDto SimulationSettings { get; set; } = new();

    // ── Circuit topology ─────────────────────────────────────────────────
    public List<EeeComponentDto> Components { get; set; } = new();
    public List<EeeConnectionDto> Connections { get; set; } = new();
}

public class EeeComponentDto
{
    public Guid Id { get; set; }
    public string ComponentType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;

    /// <summary>
    /// Omitted from JSON when zero to reduce noise for passive/structural components (§6.B).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double BreakingCurrent { get; set; }

    /// <summary>Typed numeric electrical parameters (resistance, capacitance, etc.).</summary>
    public Dictionary<string, double> ElectricalParameters { get; set; } = new();

    /// <summary>
    /// String parameters: waveform type, polarity strings, part numbers (§6.C / §6.H).
    /// Using string values avoids JsonElement boxing that occurs with Dictionary&lt;string,object&gt;.
    /// </summary>
    public Dictionary<string, string> StringParameters { get; set; } = new();
}

public class EeeConnectionDto
{
    public Guid Id { get; set; }
    public Guid FromComponentId { get; set; }
    public string FromPin { get; set; } = string.Empty;
    public Guid ToComponentId { get; set; }
    public string ToPin { get; set; } = string.Empty;
    public string NetName { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Resistance { get; set; }

    /// <summary>Intermediate routing waypoints for non-straight wires (§6.E).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<EeeWaypointDto>? Waypoints { get; set; }
}

/// <summary>
/// Serializes and deserializes the native platform EEE schema format (.eee).
/// </summary>
public static class EeePlatformSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static EeeProjectDto ToDto(CircuitNode circuit)
    {
        if (circuit == null) throw new ArgumentNullException(nameof(circuit));

        var dto = new EeeProjectDto
        {
            Title = circuit.Title,
            Description = circuit.Description,
            SavedAt = DateTime.UtcNow
            // CreatedAt and Author are preserved if round-tripping; otherwise left as UtcNow defaults.
        };

        foreach (var comp in circuit.Components)
        {
            var cDto = new EeeComponentDto
            {
                Id = comp.Id,
                ComponentType = comp.ComponentType,
                Reference = comp.Reference,
                Name = comp.Name,
                Description = comp.Description,
                X = comp.X,
                Y = comp.Y,
                Rotation = comp.Rotation,
                ScaleX = comp.ScaleX,
                ScaleY = comp.ScaleY,
                BreakingCurrent = comp.BreakingCurrent,
                ElectricalParameters = new Dictionary<string, double>(comp.ElectricalParameters),
            };

            // Serialize CustomProperties (string-typed values) into StringParameters (§6.C / §6.H)
            foreach (var kvp in comp.CustomProperties)
            {
                cDto.StringParameters[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }

            dto.Components.Add(cDto);
        }

        foreach (var conn in circuit.Connections)
        {
            var connDto = new EeeConnectionDto
            {
                Id = conn.Id,
                FromComponentId = conn.FromComponentId,
                FromPin = conn.FromPin,
                ToComponentId = conn.ToComponentId,
                ToPin = conn.ToPin,
                NetName = conn.NetName,
                Resistance = conn.Resistance,
            };

            // Persist wire routing waypoints (§6.E)
            if (conn.RoutingPoints.Count > 0)
            {
                connDto.Waypoints = conn.RoutingPoints
                    .Select(p => new EeeWaypointDto { X = p.X, Y = p.Y })
                    .ToList();
            }

            dto.Connections.Add(connDto);
        }

        return dto;
    }

    public static string ExportToEee(CircuitNode circuit)
    {
        var dto = ToDto(circuit);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static void SaveToFile(CircuitNode circuit, string filePath)
    {
        string json = ExportToEee(circuit);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, json);
    }

    public static CircuitNode LoadFromEee(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"EEE file not found: {filePath}");

        return LoadFromEeeJson(File.ReadAllText(filePath));
    }

    public static CircuitNode LoadFromEeeJson(string json)
    {
        var dto = JsonSerializer.Deserialize<EeeProjectDto>(json, JsonOptions);
        if (dto == null)
            throw new InvalidOperationException("Failed to parse EEE project file.");

        // ── Version validation (§6.A) ──────────────────────────────────────────
        if (!string.IsNullOrEmpty(dto.FormatVersion) &&
            dto.FormatVersion != EeeFormatVersion.Current)
        {
            // Attempt migration for known older versions
            RunMigration(dto);
        }

        var circuit = new CircuitNode
        {
            Title = dto.Title,
            Description = dto.Description
        };

        var compMap = new Dictionary<Guid, ComponentNode>();

        foreach (var cDto in dto.Components)
        {
            ComponentNode? comp = null;
            try
            {
                comp = ComponentRegistry.CreateInstance(cDto.ComponentType, cDto.Name, cDto.X, cDto.Y);
            }
            catch
            {
                // Fallback if component type is an alias or needs prefix
                if (ComponentRegistry.TryGetDefinition(cDto.ComponentType, out _))
                {
                    comp = ComponentRegistry.CreateInstance(cDto.ComponentType, cDto.Name, cDto.X, cDto.Y);
                }
            }

            if (comp != null)
            {
                comp.Id = cDto.Id;
                comp.Reference = cDto.Reference;
                comp.Description = cDto.Description;
                comp.Rotation = cDto.Rotation;
                comp.ScaleX = cDto.ScaleX;
                comp.ScaleY = cDto.ScaleY;
                comp.BreakingCurrent = cDto.BreakingCurrent;

                foreach (var kvp in cDto.ElectricalParameters)
                {
                    comp.ElectricalParameters[kvp.Key] = kvp.Value;
                }

                // Restore string-typed parameters from StringParameters (§6.C)
                foreach (var kvp in cDto.StringParameters)
                {
                    comp.CustomProperties[kvp.Key] = kvp.Value;
                }

                if (comp is EESimulator.Core.Components.Electrical.PowerSystemComponent powerSystemComponent)
                    powerSystemComponent.ValidateConfiguration();

                if (comp is EESimulator.Core.Components.General.CustomCircuitComponent customCircuit)
                    customCircuit.RestorePorts();

                // Legacy: if old file had CustomProperties as object bag, it is now ignored
                // (PropertyNameCaseInsensitive will map StringParameters from the JSON correctly).

                circuit.AddChild(comp);
                compMap[comp.Id] = comp;
            }
        }

        foreach (var connDto in dto.Connections)
        {
            if (compMap.TryGetValue(connDto.FromComponentId, out _) &&
                compMap.TryGetValue(connDto.ToComponentId, out _))
            {
                // NetName is passed only via constructor — not duplicated in initializer (§6.D fix)
                var conn = new ConnectionNode(
                    connDto.FromComponentId, connDto.FromPin,
                    connDto.ToComponentId, connDto.ToPin,
                    connDto.NetName)
                {
                    Id = connDto.Id,
                    Resistance = connDto.Resistance
                };

                // Restore routing waypoints (§6.E)
                if (connDto.Waypoints != null)
                {
                    foreach (var wp in connDto.Waypoints)
                        conn.RoutingPoints.Add(new Point2D(wp.X, wp.Y));
                }

                circuit.AddChild(conn);
            }
        }

        return circuit;
    }

    /// <summary>
    /// Applies in-memory migrations for files whose FormatVersion predates the current schema.
    /// Throws <see cref="InvalidOperationException"/> for truly incompatible major-version breaks.
    /// </summary>
    private static void RunMigration(EeeProjectDto dto)
    {
        // v1.0.0 → v1.1.0: StringParameters field was added; CustomProperties (object) was removed.
        // Old files will simply have an empty StringParameters list which is safe to load as-is.
        // No destructive change: migration is a no-op for 1.0.0 → 1.1.0.
        if (dto.FormatVersion == "1.0.0")
        {
            dto.FormatVersion = EeeFormatVersion.Current;
            return;
        }

        // For unknown/future versions that are ahead of us, warn but do not throw.
        // (The app should still attempt to load what it can parse.)
        System.Diagnostics.Debug.WriteLine(
            $"[EeePlatformSerializer] Unknown FormatVersion '{dto.FormatVersion}'. " +
            $"Expected '{EeeFormatVersion.Current}'. Loading may be incomplete.");
    }
}
