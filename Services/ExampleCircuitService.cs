using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EESimulator.Core.Models;
using EESimulator.Formats;
using EESimulator.Scripting;
using Microsoft.Maui.Storage;

namespace EEE.Services;

/// <summary>
/// Model representing a pre-built example circuit bundled with EESimulator.
/// </summary>
public class CircuitExampleItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Analog";
    public string Icon { get; set; } = "⚡";
    public string Badge { get; set; } = "Featured";
    public string Formula { get; set; } = string.Empty;
    public bool HasFormula => !string.IsNullOrWhiteSpace(Formula);
    public string Description { get; set; } = string.Empty;
    public string ComponentSummary { get; set; } = string.Empty;
    public string ResourcePath { get; set; } = string.Empty;
    public string RawEeeJson { get; set; } = string.Empty;
    public bool IsCommunity { get; set; }
    public string OwnerName { get; set; } = string.Empty;
}


/// <summary>
/// Service managing built-in example circuits exported with the application build.
/// </summary>
public static class ExampleCircuitService
{
    private static readonly List<CircuitExampleItem> _examples = new();

    static ExampleCircuitService()
    {
        InitializeDefaultExamples();
    }

    public static IReadOnlyList<CircuitExampleItem> GetExamples() => _examples;

    public static async Task<CircuitNode> LoadCircuitAsync(CircuitExampleItem example)
    {
        string json = string.Empty;

        // 1. Try reading the embedded app package asset
        try
        {
            if (!string.IsNullOrEmpty(example.ResourcePath))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(example.ResourcePath);
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync();
            }
        }
        catch
        {
            // Fallback to embedded json string
            json = example.RawEeeJson;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            json = example.RawEeeJson;
        }

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"No schematic content found for example '{example.Title}'.");

        var dto = JsonSerializer.Deserialize<EeeProjectDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dto == null)
            throw new InvalidOperationException("Failed to deserialize EEE example schematic.");

        // Construct circuit from DTO
        string tempFile = Path.Combine(FileSystem.CacheDirectory, $"{example.Id}.eee");
        File.WriteAllText(tempFile, json);
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(tempFile);
        EnsureLogicalPlacements(circuit);
        foreach (LogicalGridPlacement placement in LogicalGridSchematicLayout.Calculate(circuit).Placements)
        {
            placement.Component.X = placement.X;
            placement.Component.Y = placement.Y;
        }
        return circuit;
    }

    internal static void EnsureLogicalPlacements(CircuitNode circuit)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        var occupiedCells = circuit.Components
            .Where(component => component.HasLogicalPlacement)
            .Select(component => (Row: component.LogicalRow!.Value, Column: component.LogicalColumn!.Value))
            .ToHashSet();
        int lastColumn = occupiedCells.Count == 0 ? 1 : occupiedCells.Max(cell => cell.Column);

        int row = 1;
        int column = 1;
        foreach (var component in circuit.Components.Where(component => !component.HasLogicalPlacement))
        {
            while (occupiedCells.Contains((row, column)))
            {
                column++;
                if (column > lastColumn)
                {
                    row++;
                    column = 1;
                }
            }

            component.LogicalRow = row;
            component.LogicalColumn = column;
            occupiedCells.Add((row, column));
            column++;
        }
    }

    public static async Task<string> ExportExampleAsync(CircuitExampleItem example, string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string destPath = Path.Combine(targetDirectory, $"{example.Id}.eee");
        string json = string.Empty;

        try
        {
            if (!string.IsNullOrEmpty(example.ResourcePath))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(example.ResourcePath);
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync();
            }
        }
        catch
        {
            json = example.RawEeeJson;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            json = example.RawEeeJson;
        }

        await File.WriteAllTextAsync(destPath, json);
        return destPath;
    }

    private static void InitializeDefaultExamples()
    {
        _examples.Add(new CircuitExampleItem
        {
            Id = "complex_linked_component_showcase",
            Title = "Complex Linked Circuit - Full Component Showcase",
            Category = "Digital Logic",
            Icon = "🧩",
            Badge = "FULL SHOWCASE",
            Description = "A large interactive circuit based on ComplexLinkedCircuitTests, combining analog sources and loads, controlled sources, logic gates, arithmetic, sequential logic, RAM, ROM, displays, and a 7400 TTL package. Independent supply sources are arranged across the upper row, with the connected circuit blocks below.",
            ComponentSummary = "248 components · analog, digital logic, arithmetic, memory, displays, and TTL · 2 validation probes",
            ResourcePath = "Examples/complex_linked_component_showcase.eee"
        });

        // ── 1. RC Phase-Shift Oscillator ─────────────────────────────────────
        _examples.Add(new CircuitExampleItem
        {
            Id = "rc_oscillator",
            Title = "RC Phase-Shift Oscillator",
            Category = "Oscillators",
            Icon = "∿",
            Badge = "RECOMMENDED",
            Formula = "f_osc = 1 / (2π · R · C · √6) ≈ 649.7 Hz",
            Description = "Classic 3-stage RC ladder phase-shift oscillator generating sustained sinusoidal oscillations. Each RC stage contributes 60° phase shift at resonance (180° total), combined with 180° phase inversion from the common-emitter BJT amplifier to meet the Barkhausen oscillation criterion (A·β = 1, ∠360°).",
            ComponentSummary = "1x NPN BJT (β=150), 1x 12V VCC, 4x 10kΩ Resistors, 1x 47kΩ Resistor, 1x 4.7kΩ Rc, 1x 1kΩ Re, 3x 10nF Caps, 1x 10µF Ce",
            ResourcePath = "Examples/rc_oscillator.eee",
            RawEeeJson = @"{
  ""FileType"": ""EEE_PLATFORM_SCHEMATIC"",
  ""FormatVersion"": ""1.0.0"",
  ""Application"": ""EESimulator"",
  ""Title"": ""RC Phase-Shift Oscillator"",
  ""Description"": ""Classic 3-stage RC ladder phase-shift oscillator generating sustained sinusoidal oscillations."",
  ""SavedAt"": ""2026-11-01T20:30:00Z"",
  ""Components"": [
    { ""Id"": ""11111111-1111-1111-1111-111111110001"", ""ComponentType"": ""VoltageSource"", ""Reference"": ""VCC"", ""Name"": ""VCC"", ""Description"": ""Main DC Power Supply (12V)"", ""StringParameters"": { ""LogicalRow"": ""4"", ""LogicalColumn"": ""1"" }, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 2.0, ""ElectricalParameters"": { ""Voltage"": 12.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110002"", ""ComponentType"": ""Ground"", ""Reference"": ""GND1"", ""Name"": ""GND1"", ""Description"": ""Circuit Ground Reference"", ""StringParameters"": { ""LogicalRow"": ""9"", ""LogicalColumn"": ""1"" }, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.0, ""ElectricalParameters"": {}, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110003"", ""ComponentType"": ""Resistor"", ""Reference"": ""R1"", ""Name"": ""R1"", ""Description"": ""Upper Base Bias Resistor (47kΩ)"", ""StringParameters"": { ""LogicalRow"": ""2"", ""LogicalColumn"": ""2"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 47000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110004"", ""ComponentType"": ""Resistor"", ""Reference"": ""R2"", ""Name"": ""R2"", ""Description"": ""Lower Base Bias Resistor (10kΩ)"", ""StringParameters"": { ""LogicalRow"": ""7"", ""LogicalColumn"": ""2"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110005"", ""ComponentType"": ""NPN"", ""Reference"": ""Q1"", ""Name"": ""Q1"", ""Description"": ""Common-Emitter Amplifier NPN BJT (β=150)"", ""StringParameters"": { ""LogicalRow"": ""5"", ""LogicalColumn"": ""3"" }, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.5, ""ElectricalParameters"": { ""Bf"": 150.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110006"", ""ComponentType"": ""Resistor"", ""Reference"": ""Rc"", ""Name"": ""Rc"", ""Description"": ""Collector Load Resistor (4.7kΩ)"", ""StringParameters"": { ""LogicalRow"": ""1"", ""LogicalColumn"": ""4"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.2, ""ElectricalParameters"": { ""Resistance"": 4700.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110007"", ""ComponentType"": ""Resistor"", ""Reference"": ""Re"", ""Name"": ""Re"", ""Description"": ""Emitter Bias Stabilization Resistor (1kΩ)"", ""StringParameters"": { ""LogicalRow"": ""8"", ""LogicalColumn"": ""4"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.2, ""ElectricalParameters"": { ""Resistance"": 1000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110008"", ""ComponentType"": ""Capacitor"", ""Reference"": ""Ce"", ""Name"": ""Ce"", ""Description"": ""Emitter AC Bypass Capacitor (10µF)"", ""StringParameters"": { ""LogicalRow"": ""8"", ""LogicalColumn"": ""5"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.5, ""ElectricalParameters"": { ""Capacitance"": 1e-05 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110009"", ""ComponentType"": ""Capacitor"", ""Reference"": ""C1"", ""Name"": ""C1"", ""Description"": ""RC Phase Shift Network Stage 1 Capacitor (10nF)"", ""StringParameters"": { ""LogicalRow"": ""3"", ""LogicalColumn"": ""6"" }, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Capacitance"": 1e-08 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110010"", ""ComponentType"": ""Resistor"", ""Reference"": ""R3"", ""Name"": ""R3"", ""Description"": ""RC Phase Shift Network Stage 1 Resistor (10kΩ)"", ""StringParameters"": { ""LogicalRow"": ""6"", ""LogicalColumn"": ""7"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110011"", ""ComponentType"": ""Capacitor"", ""Reference"": ""C2"", ""Name"": ""C2"", ""Description"": ""RC Phase Shift Network Stage 2 Capacitor (10nF)"", ""StringParameters"": { ""LogicalRow"": ""3"", ""LogicalColumn"": ""8"" }, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Capacitance"": 1e-08 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110012"", ""ComponentType"": ""Resistor"", ""Reference"": ""R4"", ""Name"": ""R4"", ""Description"": ""RC Phase Shift Network Stage 2 Resistor (10kΩ)"", ""StringParameters"": { ""LogicalRow"": ""6"", ""LogicalColumn"": ""9"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110013"", ""ComponentType"": ""Capacitor"", ""Reference"": ""C3"", ""Name"": ""C3"", ""Description"": ""RC Phase Shift Network Stage 3 Capacitor (10nF)"", ""StringParameters"": { ""LogicalRow"": ""3"", ""LogicalColumn"": ""10"" }, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Capacitance"": 1e-08 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110014"", ""ComponentType"": ""Resistor"", ""Reference"": ""R5"", ""Name"": ""R5"", ""Description"": ""RC Phase Shift Network Stage 3 Resistor (10kΩ)"", ""StringParameters"": { ""LogicalRow"": ""6"", ""LogicalColumn"": ""11"" }, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} }
  ],
  ""Connections"": [
    { ""Id"": ""22222222-2222-2222-2222-222222220001"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110001"", ""FromPin"": ""negative"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110002"", ""ToPin"": ""gnd"", ""NetName"": ""0"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220002"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110001"", ""FromPin"": ""positive"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110003"", ""ToPin"": ""pin1"", ""NetName"": ""VCC_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220003"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110003"", ""FromPin"": ""pin1"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110006"", ""ToPin"": ""pin1"", ""NetName"": ""VCC_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220004"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110003"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110004"", ""ToPin"": ""pin1"", ""NetName"": ""BASE_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220005"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110004"", ""FromPin"": ""pin1"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110005"", ""ToPin"": ""base"", ""NetName"": ""BASE_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220006"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110004"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110002"", ""ToPin"": ""gnd"", ""NetName"": ""0"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220007"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110006"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110005"", ""ToPin"": ""collector"", ""NetName"": ""COLLECTOR_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220008"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110005"", ""FromPin"": ""emitter"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110007"", ""ToPin"": ""pin1"", ""NetName"": ""EMITTER_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220009"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110007"", ""FromPin"": ""pin1"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110008"", ""ToPin"": ""pin1"", ""NetName"": ""EMITTER_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220010"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110007"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110002"", ""ToPin"": ""gnd"", ""NetName"": ""0"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220011"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110008"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110002"", ""ToPin"": ""gnd"", ""NetName"": ""0"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220012"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110005"", ""FromPin"": ""collector"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110009"", ""ToPin"": ""pin1"", ""NetName"": ""COLLECTOR_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220013"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110009"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110010"", ""ToPin"": ""pin1"", ""NetName"": ""STAGE1_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220014"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110010"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110002"", ""FromPin"": ""pin2"", ""ToPin"": ""gnd"", ""NetName"": ""0"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220015"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110009"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110011"", ""ToPin"": ""pin1"", ""NetName"": ""STAGE1_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220016"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110011"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110012"", ""ToPin"": ""pin1"", ""NetName"": ""STAGE2_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220017"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110012"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110002"", ""ToPin"": ""gnd"", ""NetName"": ""0"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220018"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110011"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110013"", ""ToPin"": ""pin1"", ""NetName"": ""STAGE2_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220019"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110013"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110014"", ""ToPin"": ""pin1"", ""NetName"": ""STAGE3_NET"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220020"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110014"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110002"", ""ToPin"": ""gnd"", ""NetName"": ""0"", ""Resistance"": 0.0 },
    { ""Id"": ""22222222-2222-2222-2222-222222220021"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110013"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110005"", ""ToPin"": ""base"", ""NetName"": ""STAGE3_NET"", ""Resistance"": 0.0 }
  ]
}"
        });

        // ── 2. BJT Common Emitter Amplifier ─────────────────────────────────
        _examples.Add(new CircuitExampleItem
        {
            Id = "bjt_ce_amplifier",
            Title = "BJT Common Emitter Amplifier",
            Category = "Amplifiers",
            Icon = "🔊",
            Badge = "POPULAR",
            Formula = "A_v ≈ -Rc / re' | Gain ≈ -30x",
            Description = "Standard voltage-divider biased Common Emitter BJT small-signal amplifier with emitter degeneration resistor, bypass capacitor, and AC input/output coupling for audio and sensor amplification.",
            ComponentSummary = "1x NPN BJT (2N3904), 1x 15V VCC, 1x 10mV AC Vin, 1x 33kΩ R1, 1x 6.8kΩ R2, 1x 3.3kΩ Rc, 1x 680Ω Re, 2x 10µF Caps, 1x 10kΩ RL",
            ResourcePath = "Examples/bjt_ce_amplifier.eee"
        });

        // ── 3. Digital Logic Half Adder ──────────────────────────────────────
        _examples.Add(new CircuitExampleItem
        {
            Id = "logic_half_adder",
            Title = "Digital Logic Half Adder",
            Category = "Digital Logic",
            Icon = "⊕",
            Badge = "LOGIC",
            Formula = "Sum = A ⊕ B  |  Carry = A · B",
            Description = "Fundamental digital arithmetic combinational half adder circuit built with XOR gate for Sum output and AND gate for Carry output, accepting two single-bit binary inputs with interactive live logic output displays.",
            ComponentSummary = "2x Interactive Logic Inputs, 1x 2-Input XOR Gate, 1x 2-Input AND Gate, 2x Interactive Logic Output Displays",
            ResourcePath = "Examples/logic_half_adder.eee"
        });

        _examples.Add(new CircuitExampleItem
        {
            Id = "voltage_divider",
            Title = "Resistive Voltage Divider",
            Category = "Passive Circuits",
            Icon = "½",
            Badge = "STARTER",
            Formula = "Vout = Vin · R2 / (R1 + R2)",
            Description = "Explore how two series resistors divide a source voltage. Equal 10 kΩ resistors produce approximately half the input voltage at their midpoint.",
            ComponentSummary = "1x 5 V Voltage Source, 2x 10 kΩ Resistors, 1x Ground",
            ResourcePath = "Examples/voltage_divider.eee"
        });

        _examples.Add(new CircuitExampleItem
        {
            Id = "rc_low_pass",
            Title = "RC Low-Pass Filter",
            Category = "Filters",
            Icon = "⌁",
            Badge = "FILTER",
            Formula = "fc = 1 / (2πRC) ≈ 1.59 kHz",
            Description = "A first-order passive RC network that passes low-frequency signals and attenuates high-frequency signals.",
            ComponentSummary = "1x 1 V Source, 1x 1 kΩ Resistor, 1x 100 nF Capacitor, 1x Ground",
            ResourcePath = "Examples/rc_low_pass.eee"
        });

        _examples.Add(new CircuitExampleItem
        {
            Id = "ttl_nand_gate",
            Title = "74LS00 NAND Gate",
            Category = "Digital Logic",
            Icon = "⊼",
            Badge = "TTL",
            Formula = "Y = ¬(A · B)",
            Description = "Connect two interactive logic inputs to one NAND gate inside a 7400 quad TTL package and observe its output.",
            ComponentSummary = "2x Logic Inputs, 1x 7400 TTL IC, 1x Logic Output",
            ResourcePath = "Examples/ttl_nand_gate.eee"
        });

        _examples.Add(new CircuitExampleItem
        {
            Id = "alu_ram_counter",
            Title = "8-bit ALU with RAM and Address Counter",
            Category = "Digital Logic",
            Icon = "∑",
            Badge = "CPU DEMO",
            Description = "An interactive four-bit add/subtract demo with zero-extended inputs, an 8-bit ALU, clocked RAM, and a four-bit address counter. A 5 V rail enables RAM output; hexadecimal displays show the ALU result.",
            ComponentSummary = "11x Logic Inputs, 2x Logic Constants, 1x 5 V Power Rail, 8x XOR Gates, 1x 8-bit Adder, 1x RAM, 1x 4-bit Counter, 1x Clock, 2x Hex Displays",
            ResourcePath = "Examples/alu_ram_counter.eee"
        });

        var additionalExamples = new (string Id, string Title, string Category, string Description, string ComponentSummary)[]
        {
            ("wien_bridge_oscillator", "Wien Bridge Oscillator", "Oscillators", "Wien bridge RC frequency-selective network driven by a sinusoidal source. The simulator demonstrates the bridge response; it does not model automatic amplitude stabilization.", "Sine source, Wien RC network, load, and test point"),
            ("hartley_oscillator", "Hartley Oscillator", "Oscillators", "Hartley LC tank teaching circuit with a sinusoidal excitation source. The source initializes the response; mutual coupling between tapped inductors is not modeled.", "Sine source, tapped-inductor tank, damping resistor, and test point"),
            ("colpitts_oscillator", "Colpitts Oscillator", "Oscillators", "Colpitts LC tank demonstration using a capacitive divider and sinusoidal excitation. Transistor startup and amplitude limiting are not represented.", "Sine source, inductor, capacitive divider, and test point"),
            ("555_astable_oscillator", "555 Astable Oscillator", "Oscillators", "555 astable timing demonstration. A pulse source represents the timer output; the external timing network is shown, but the internal 555 comparator and latch are not modeled.", "Pulse source, timing resistors and capacitor, lamp, and test points"),
            ("half_wave_rectifier", "Half-Wave Rectifier", "Passive Circuits", "Single-diode half-wave rectifier with a resistive lamp load, smoothing capacitor, and output test point.", "AC source, diode, load, filter capacitor, lamp, and test point"),
            ("full_wave_bridge_rectifier", "Full-Wave Bridge Rectifier", "Passive Circuits", "Four-diode bridge rectifier with AC input, resistive load, filter capacitor, and output test point.", "AC source, four diodes, load, filter capacitor, and test point"),
            ("three_phase_bridge_rectifier", "Three-Phase Bridge Rectifier", "Passive Circuits", "Six-pulse three-phase diode bridge with phase-shifted sources, DC load, filter capacitor, and bus test point.", "Three AC phases, six diodes, DC load, filter capacitor, and test point"),
            ("adjustable_dc_power_supply", "Adjustable DC Power Supply", "Passive Circuits", "Adjustable source and loaded resistor-divider study. Edit the source and divider values to explore output changes; this is not a dedicated regulator IC model.", "Adjustable DC source, divider, output capacitor, load, and test point"),
            ("7805_regulated_power_supply", "7805 Regulated Power Supply", "Passive Circuits", "7805 application study with input and output decoupling, load, and nominal 5 V output. The regulator is represented by its nominal output source because no 7805 device model is registered.", "9 V input, nominal 5 V regulator equivalent, decoupling, LED, and test point"),
            ("buck_converter", "Buck Converter", "Passive Circuits", "Idealized buck converter with PWM-equivalent switch-node source, freewheel diode, output inductor, capacitor, and load.", "PWM source, diode, inductor, output capacitor, load, and test point"),
            ("boost_converter", "Boost Converter", "Passive Circuits", "Boost converter teaching circuit with inductor, pulse-driven low-side MOSFET, rectifier diode, output capacitor, and load.", "DC source, pulse driver, MOSFET, diode, inductor, capacitor, load, and test point"),
            ("buck_boost_converter", "Buck-Boost Converter", "Passive Circuits", "Inverting buck-boost teaching circuit using a pulse-driven MOSFET, inductor, diode, output storage network, and test points.", "DC source, pulse driver, MOSFET, diode, inductor, capacitor, load, and test points"),
            ("flyback_smps", "Flyback SMPS", "Passive Circuits", "Flyback converter block diagram using primary PWM excitation and an idealized isolated secondary source. Transformer coupling and switching losses are not modeled.", "PWM source, primary inductor, idealized secondary, diode, output filter, and test point"),
            ("laptop_charger_smps", "Laptop Charger SMPS", "Passive Circuits", "Laptop adapter concept with isolated 19.5 V output equivalent, output filtering, and load. The high-frequency transformer and control loop are idealized, not a safety-rated charger model.", "PWM source, idealized transformer stages, output diode, filter, load, and test point"),
            ("lithium_ion_battery_charger", "Lithium-Ion Battery Charger", "Passive Circuits", "Single-cell low-current charging demonstration with a current-limiting resistor, battery model, status LED, and test point. It is not a complete safety charger.", "5 V source, current limiter, battery, status LED, and test point"),
            ("single_phase_h_bridge_inverter", "Single-Phase H-Bridge Inverter", "Passive Circuits", "Single-phase H-bridge output study represented by a bipolar PWM-equivalent source, LC filter, and lamp load. Dead time and device losses are omitted.", "DC bus, PWM-equivalent source, LC filter, lamp, and test point"),
            ("three_phase_inverter", "Three-Phase Inverter", "Passive Circuits", "Three-phase output demonstration with phase-shifted sources and balanced phase loads. Gate-drive dead time and semiconductor losses are outside this model.", "Three phase-shifted sources, phase loads, and three test points"),
            ("class_ab_audio_amplifier", "Class AB Audio Amplifier", "Amplifiers", "Complementary push-pull output-stage teaching circuit with bias resistors, resistive load, and audio-frequency input.", "NPN/PNP output stage, bias network, audio source, load, and test point"),
            ("operational_amplifier", "Operational Amplifier", "Amplifiers", "Inverting op-amp stage with input resistor, feedback resistor, bipolar supplies, AC signal, and output test point.", "Op-amp, feedback network, bipolar supplies, load, and test point"),
            ("four_bit_adder", "Four-Bit Adder", "Digital Logic", "Interactive four-bit adder with toggleable A/B inputs, carry input, hexadecimal sum display, and carry output.", "Nine toggle inputs, 4-bit adder, hex display, logic output, and test point"),
            ("four_bit_alu", "Four-Bit ALU", "Digital Logic", "Interactive four-bit arithmetic unit with separate add and subtract paths, toggleable operands, and hexadecimal result displays. This is an educational datapath, not a monolithic ALU IC.", "Toggle inputs, 4-bit adder and subtractor, two hex displays, and test points"),
            ("four_bit_cpu", "Four-Bit CPU", "Digital Logic", "Interactive four-bit datapath demonstration combining toggleable data inputs, an adder, clocked register, counter, and hexadecimal output.", "Toggle inputs, clock, 4-bit register, counter, adder, hex display, and test point"),
            ("eight_bit_cpu", "Eight-Bit CPU", "Digital Logic", "Interactive eight-bit datapath demonstration combining toggleable data inputs, an adder, clocked register, counter, and two hexadecimal output digits.", "Toggle inputs, clock, 8-bit register, counter, adder, two hex displays, and test point"),
            ("three_phase_transformer", "Three-Phase Transformer", "Passive Circuits", "Three-phase Y-Y transformer model fed by a balanced source set. Secondary phase loads and test points expose turns-ratio and phase behavior.", "Three phase-shifted sources, transformer, phase loads, and test point"),
            ("three_phase_synchronous_generator", "Three-Phase Synchronous Generator", "Passive Circuits", "Synchronous generator model with three phase terminals, neutral reference, resistive phase loads, and phase-voltage test point.", "Synchronous generator, neutral, phase loads, and test point"),
            ("three_phase_induction_motor", "Three-Phase Induction Motor", "Passive Circuits", "Three-phase induction motor equivalent model with balanced phase supplies, neutral return, and phase test point.", "Three-phase supply, induction motor model, neutral, and test point"),
            ("star_delta_motor_starter", "Star-Delta Motor Starter", "Passive Circuits", "Star-delta starter teaching diagram with an induction motor, three-phase supply, and interactive start input. The contactor interlock sequence is represented conceptually, not as a switching transient.", "Three-phase supply, induction motor, start toggle, status indicator, and test point"),
            ("complete_solar_power_system", "Complete Solar Power System", "Passive Circuits", "Solar panel, battery storage, DC load, and output test point demonstrate a small stand-alone solar system. Irradiance and battery state of charge can be edited; charge protection is not included.", "Solar panel, battery, load, status LED, and test point"),
            ("generation_transmission_network", "Electrical Generation and Transmission Network", "Passive Circuits", "End-to-end power-system study with a synchronous generator, three-phase transmission line, transformer, and balanced phase loads. Test points expose generator and load phase nodes.", "Synchronous generator, transmission line, transformer, balanced loads, and test points"),
            ("crystal_oscillator", "Crystal Oscillator", "Oscillators", "Quartz crystal frequency-selection demonstration with a resonant network, adjustable drive source, and input/output voltage probes. The crystal is represented by its equivalent electrical network.", "Signal source, crystal equivalent RLC network, output load, and test probes"),
            ("555_timer_monostable", "555 Timer Monostable", "Oscillators", "One-shot timing circuit with a trigger input, timing resistor-capacitor network, pulse output, indicator lamp, and test points. The timer IC behavior is represented by an equivalent pulse source.", "Trigger source, timing resistor and capacitor, pulse output, lamp, and probes"),
            ("astable_multivibrator", "Astable Multivibrator", "Oscillators", "Transistor multivibrator timing study with an adjustable pulse drive, RC timing network, indicator, and output probes.", "Pulse source, transistor-stage equivalent, RC timing network, lamp, and probes"),
            ("voltage_controlled_oscillator", "Voltage-Controlled Oscillator (VCO)", "Oscillators", "VCO teaching circuit with an adjustable control-voltage source, frequency-selective LC network, output load, and measurement probes.", "Control source, LC tank equivalent, output load, and probes"),
            ("sepic_converter", "SEPIC Converter", "Power Electronics", "SEPIC topology study with coupled energy-transfer stages, a switching-drive equivalent, output rectification, storage, and load. It is an educational low-voltage model, not a power-rated design.", "DC source, inductors, coupling capacitor, switching equivalent, diode, output filter, and probes"),
            ("forward_converter", "Forward Converter", "Power Electronics", "Isolated forward-converter concept using a low-voltage switching equivalent, transformer-stage equivalent, rectification, output filter, and load.", "Pulse source, transformer-stage equivalent, diode, output filter, load, and probes"),
            ("push_pull_converter", "Push-Pull Converter", "Power Electronics", "Push-pull converter study with alternating drive sources, a transformer-stage equivalent, secondary rectification, output filter, and test nodes.", "Two-phase drive, transformer-stage equivalent, rectifier, output filter, and probes"),
            ("full_bridge_smps", "Full-Bridge SMPS", "Power Electronics", "Full-bridge switching-supply concept with a low-voltage bridge-drive equivalent, isolated stage, secondary rectification, output filter, and load.", "Bridge-drive equivalent, transformer stage, rectifier, output filter, and probes"),
            ("three_phase_controlled_rectifier", "Three-Phase Controlled Rectifier", "Power Electronics", "Three-phase controlled-rectifier study with phase sources, gated rectifier-stage equivalents, a DC load, and phase/DC test points.", "Three phase sources, controlled rectifier equivalents, DC load, and probes"),
            ("power_factor_correction", "Power Factor Correction (PFC)", "Power Electronics", "Boost-PFC concept with an AC input equivalent, input filter, switching-stage equivalent, DC link, and input/output measurement nodes.", "AC source, input filter, boost-stage equivalent, DC link, and probes"),
            ("llc_resonant_converter", "LLC Resonant Converter", "Power Electronics", "LLC resonant-converter study using a resonant inductor-capacitor network, transformer-stage equivalent, output rectification, and load.", "Resonant L-C network, transformer-stage equivalent, rectifier, load, and probes"),
            ("cockcroft_walton_multiplier", "Cockcroft-Walton Voltage Multiplier", "Power Electronics", "Two-stage Cockcroft-Walton multiplier demonstrating cascaded diode-capacitor charge transfer from a low-voltage AC source. This low-voltage teaching circuit is not a high-voltage supply.", "AC source, diode-capacitor ladder, load, and stage probes"),
            ("common_source_mosfet_amplifier", "Common-Source MOSFET Amplifier", "Amplifiers", "Small-signal common-source amplifier study with an adjustable input, MOSFET-stage equivalent, drain load, bypass network, and input/output probes.", "AC input, MOSFET-stage equivalent, bias and load network, and probes"),
            ("differential_amplifier", "Differential Amplifier", "Amplifiers", "Differential-input amplifier study with two adjustable signal inputs, matched input paths, a shared-tail equivalent, output load, and probes.", "Two signal sources, matched input network, amplifier-stage equivalent, load, and probes"),
            ("instrumentation_amplifier", "Instrumentation Amplifier", "Amplifiers", "Precision differential measurement study with two adjustable inputs, high-input-impedance paths, gain network, output load, and test probes.", "Two signal sources, input/gain network, output load, and probes"),
            ("class_d_audio_amplifier", "Class D Audio Amplifier", "Amplifiers", "Switching audio-output study with an audio-frequency input, pulse-drive equivalent, LC reconstruction filter, low-voltage load, and probes.", "Audio input, switching-drive equivalent, LC filter, load, and probes"),
            ("active_band_pass_filter", "Active Band-Pass Filter", "Filters", "Active band-pass response study with an adjustable signal source, cascaded high- and low-frequency shaping networks, output load, and test points.", "Signal source, R-C frequency-selective network, amplifier-stage equivalent, and probes"),
            ("eight_bit_alu", "Eight-Bit ALU", "Digital Logic", "Interactive eight-bit arithmetic datapath with toggleable A/B operands, carry input, an eight-bit adder core, carry output, and hexadecimal digit outputs.", "17 toggle inputs, 8-bit adder core, 9 logic outputs, and analog monitor probes"),
            ("sixteen_by_four_ram", "16 × 4 RAM", "Digital Logic", "Interactive 16-address by four-bit RAM with toggleable address/data inputs, write-enable and clock controls, four data outputs, and monitor points.", "4 address toggles, 4 data toggles, clock/write controls, 4-bit RAM, and outputs"),
            ("digital_calculator", "Digital Calculator", "Digital Logic", "Interactive two-operand arithmetic demonstration using toggleable binary inputs, an adder datapath, carry indication, and a hexadecimal result display.", "Operand toggles, adder datapath, carry output, hexadecimal display, and probes"),
            ("digital_clock", "Digital Clock", "Digital Logic", "Clock-and-counter demonstration with adjustable clock frequency, a four-bit counter, toggleable reset/enable controls, and a hexadecimal digit display.", "Clock, counter, reset/enable toggles, hexadecimal display, and probes"),
            ("traffic_light_controller", "Traffic Light Controller", "Digital Logic", "Interactive traffic-signal logic demonstration with a clock input, toggleable phase selection, and red/amber/green indicator outputs.", "Clock, phase toggles, logic gates, three signal indicators, and probes"),
            ("single_phase_induction_motor", "Single-Phase Induction Motor", "Electrical Machines", "Low-voltage equivalent-circuit study of a single-phase induction motor with adjustable AC supply, winding impedance, load, and voltage/current test points.", "AC source, winding R-L equivalent, load, and probes"),
            ("dc_shunt_motor", "DC Shunt Motor", "Electrical Machines", "DC shunt-motor equivalent circuit showing armature and parallel field branches, an adjustable DC supply, load, and branch test points.", "DC source, armature and shunt-field R-L branches, load, and probes"),
            ("dc_series_motor", "DC Series Motor", "Electrical Machines", "DC series-motor equivalent circuit showing the armature and series field in one current path, with adjustable supply and measurement nodes.", "DC source, series field and armature R-L equivalent, load, and probes"),
            ("single_phase_transformer", "Single-Phase Transformer", "Electrical Machines", "Single-phase transformer teaching model with primary/secondary winding equivalents, adjustable AC input, a secondary load, and voltage probes.", "AC source, transformer winding equivalent, secondary load, and probes"),
            ("three_phase_star_delta_connection", "Three-Phase Star-Delta Connection", "Electrical Machines", "Interactive three-phase connection study with balanced phase sources and switchable star/delta wiring configurations represented as low-voltage equivalent branches.", "Three phase sources, star/delta branch network, toggle inputs, and probes"),
            ("mppt_solar_charge_controller", "MPPT Solar Charge Controller", "Renewable Energy", "Solar charge-control study with adjustable panel-equivalent source, converter stage, battery/load equivalent, and input/output probes. The control loop is represented conceptually.", "Adjustable solar source, converter-stage equivalent, battery/load, and probes"),
            ("automatic_generator_changeover", "Automatic Generator Changeover", "Industrial Control", "Low-voltage changeover-control demonstration with utility and generator inputs, a toggleable source-select control, load indicators, and test points.", "Utility/generator sources, selectable changeover equivalent, load lamp, and probes"),
            ("wien_bridge_function_generator", "Wien Bridge Function Generator", "Oscillators", "Adjustable-frequency Wien bridge signal-generation study with a balanced RC network, output load, and input/output probes. The source provides the oscillation drive; automatic amplitude stabilization is not modeled.", "Adjustable sine source, Wien RC network, output load, and test probes"),
            ("sawtooth_wave_generator", "Sawtooth Wave Generator", "Oscillators", "Sawtooth timing demonstration using a periodic ramp-equivalent source, load indicator, and waveform measurement points.", "Ramp-equivalent source, load, and waveform probes"),
            ("triangle_wave_generator", "Triangle Wave Generator", "Oscillators", "Triangle-wave timing demonstration with an adjustable periodic source, output load, and waveform probes. The integrator/comparator stages are represented by the source waveform.", "Triangle-equivalent source, load, and waveform probes"),
            ("pll_frequency_synthesizer", "PLL Frequency Synthesizer", "Oscillators", "Phase-locked-loop teaching block with adjustable reference, phase-control input, oscillator output equivalent, and frequency-monitoring test points. The lock loop is conceptual.", "Reference source, phase control, oscillator equivalent, and probes"),
            ("single_phase_controlled_bridge_rectifier", "Single-Phase Controlled Bridge Rectifier", "Power Electronics", "Low-voltage single-phase bridge rectifier study with four diode equivalents, a toggleable firing-enable input, DC load, and measurement nodes. Thyristor firing-angle dynamics are not modeled.", "AC source, four rectifier diodes, gate-enable toggle, load, and probes"),
            ("three_phase_half_wave_rectifier", "Three-Phase Half-Wave Rectifier", "Power Electronics", "Balanced three-phase low-voltage half-wave rectifier with three phase rectifiers, neutral return, DC load, and phase/output test points.", "Three phase sources, three diode equivalents, load, and probes"),
            ("three_phase_ac_voltage_controller", "Three-Phase AC Voltage Controller", "Power Electronics", "Three-phase voltage-control teaching circuit with balanced low-voltage sources, per-phase controlled-switch equivalents, a toggleable control input, and phase probes. Thyristor phase control is simplified.", "Three-phase sources, switch equivalents, control toggle, and probes"),
            ("single_phase_cycloconverter", "Single-Phase Cycloconverter", "Power Electronics", "Low-voltage frequency-conversion concept with an adjustable AC source, toggle-controlled output path, series load network, and source/output test points. Frequency conversion is represented by the source waveform.", "AC source, mode toggle, series network, load, and probes"),
            ("synchronous_buck_converter", "Synchronous Buck Converter", "Power Electronics", "Low-voltage PWM-equivalent converter study with a toggle-controlled switch, output inductor/capacitor network, load, and input/output probes. Synchronous switch timing is conceptual.", "Pulse source, switch equivalent, LC output filter, load, and probes"),
            ("zeta_converter", "Zeta DC-DC Converter", "Power Electronics", "Low-voltage Zeta energy-transfer teaching network with pulse-equivalent source, inductor-capacitor elements, rectifier, load, and test points. Closed-loop switching behavior is simplified.", "Pulse source, L-C transfer network, rectifier, load, and probes"),
            ("dc_voltage_doubler", "DC Voltage Doubler", "Power Electronics", "Low-voltage diode-capacitor voltage doubler with AC input, two charge-storage capacitors, output load, and stage-voltage test points.", "AC source, two diode-capacitor stages, load, and probes"),
            ("active_clamp_flyback_converter", "Active Clamp Flyback Converter", "Power Electronics", "Low-voltage flyback/clamp teaching network with pulse drive, primary inductance, clamp storage, rectification, output filter, and probes. Transformer isolation and clamp switching are not modeled; this is not a mains or safety-rated supply.", "Pulse source, primary inductance, clamp capacitor, rectifier, output filter, and probes"),
            ("class_a_power_amplifier", "Class A Power Amplifier", "Amplifiers", "Low-voltage common-emitter Class A output stage with adjustable signal input, transistor bias network, collector load, and input/output probes.", "NPN transistor, bias resistors, AC input, collector load, and probes"),
            ("class_b_push_pull_amplifier", "Class B Push-Pull Amplifier", "Amplifiers", "Complementary low-voltage NPN/PNP emitter-follower output stage with an AC input, resistive load, and output probe. Bias crossover behavior is not modeled.", "Complementary transistor pair, AC input, load, and probes"),
            ("class_c_rf_amplifier", "Class C RF Amplifier", "Amplifiers", "RF tuned-stage teaching circuit with a pulse/sine drive, transistor switch, LC collector tank, low-voltage load, and waveform probes.", "NPN transistor, tuned LC tank, RF drive, load, and probes"),
            ("schmitt_trigger", "Schmitt Trigger", "Analog Electronics", "Hysteresis/threshold teaching network with adjustable input, toggle-controlled output path, load indicator, and input/output test points. The active comparator stage is represented conceptually.", "Signal source, feedback resistors, toggle control, indicator, and probes"),
            ("precision_rectifier", "Precision Rectifier", "Analog Electronics", "Low-voltage precision-rectifier teaching network with sinusoidal input, diode path, toggle-controlled output, load, and input/output probes. Op-amp feedback is not modeled.", "AC source, diode, feedback network, load, and probes"),
            ("active_notch_filter", "Active Notch Filter", "Filters", "Twin-T notch response demonstration with adjustable AC input, paired R-C networks, output load, and probes to inspect the rejected frequency band.", "AC source, twin-T R-C network, load, and frequency probes"),
            ("eight_bit_register_bank", "Eight-Bit Register Bank", "Digital Logic", "Interactive two-register, eight-bit bank with toggleable data inputs, shared clock/enable/clear controls, hexadecimal displays, and register-output probes.", "Eight toggle inputs, two 8-bit registers, shared controls, displays, and probes"),
            ("eight_bit_shift_register", "Eight-Bit Shift Register", "Digital Logic", "Interactive serial-in/parallel-out eight-bit shift register with toggleable serial data, clock/enable/reset, LED-bar outputs, and serial/parallel probes.", "Serial input, clock and control toggles, shift register, and outputs"),
            ("eight_bit_binary_counter", "Eight-Bit Binary Counter", "Digital Logic", "Eight-bit clocked binary counter with toggleable enable/direction/reset, clock input, two hexadecimal digit displays, and carry/output probes.", "Clock, three toggle controls, 8-bit counter, displays, and probes"),
            ("seven_segment_display_decoder", "Seven-Segment Display Decoder", "Digital Logic", "Four-bit interactive input drives a hexadecimal digit display, which decodes the nibble into seven-segment patterns. Includes input and segment-output test points.", "Four toggle inputs, hexadecimal seven-segment display, and probes"),
            ("digital_stopwatch", "Digital Stopwatch", "Digital Logic", "Clocked two-digit stopwatch demonstration with toggleable run/reset controls, cascaded counters, hexadecimal digit displays, and count/carry probes.", "Clock, run/reset toggles, two counters, displays, and probes"),
            ("simple_microcontroller_system", "Simple Microcontroller System", "Digital Systems", "Educational microcontroller datapath combining a clock, small RAM, register, adder, toggleable input port, and visible output display. Instruction execution is represented as connected functional blocks.", "Clock, 4×4 RAM, 8-bit register/adder blocks, I/O toggles, and display"),
            ("permanent_magnet_dc_motor_controller", "PMDC Motor Controller", "Electrical Machines", "Low-voltage PMDC speed-control study with PWM-equivalent source, toggle-controlled switch, armature R-L equivalent, load lamp, and output probes. Mechanical speed dynamics are not modeled.", "PWM source, enable switch, motor R-L equivalent, load, and probes"),
            ("brushless_dc_motor_drive", "Brushless DC Motor Drive", "Electrical Machines", "Low-voltage BLDC drive concept with pulse excitation, toggle-controlled switch, motor R-L equivalent, visible load and probes. Electronic commutation sequencing is conceptual.", "Pulse source, enable switch, motor R-L equivalent, load, and probes"),
            ("variable_frequency_drive", "Variable Frequency Drive (VFD)", "Electrical Machines", "Low-voltage VFD concept with adjustable-frequency pulse source, toggle-controlled run path, motor R-L equivalent and output probes. Three-phase inverter control is conceptual.", "Adjustable-frequency source, run switch, motor R-L equivalent, and probes"),
            ("single_phase_ac_motor_capacitor_starter", "Single-Phase AC Motor Capacitor Starter", "Electrical Machines", "Low-voltage motor-starting study with a toggle-controlled series start path, winding R-L equivalent, load lamp and winding test points. The separate auxiliary winding and starting capacitor are not modeled.", "AC source, start toggle, winding R-L equivalent, load, and probes"),
            ("grid_tied_solar_inverter", "Grid-Tied Solar Inverter", "Renewable Energy", "Low-voltage grid-synchronization concept with solar-source equivalent, inverter-output source, grid reference, load, and synchronism probes. This is not a grid-interconnection design.", "Solar/DC source, inverter equivalent, AC reference, load, and probes"),
            ("automatic_voltage_regulator", "Automatic Voltage Regulator (AVR)", "Electrical Control", "Generator-excitation control demonstration with a synchronous-generator model, adjustable excitation input, reference/setpoint control, and voltage feedback probes. Closed-loop regulation is represented conceptually.", "Generator model, excitation control, voltage reference, and feedback probes"),
            ("butterworth_filter", "Butterworth Low-Pass Filter", "Filters", "Second-order passive RLC low-pass filter with a maximally flat Butterworth response. Input and output test points verify the DC passband.", "1 V source, series inductor, shunt capacitor and load, input/output probes"),
            ("chebyshev_filter", "Chebyshev Low-Pass Filter", "Filters", "Second-order passive RLC low-pass filter illustrating the steeper Chebyshev response and its passband ripple trade-off. Input and output test points verify the DC passband.", "1 V source, series inductor, shunt capacitor and load, input/output probes"),
            ("bessel_filter", "Bessel Low-Pass Filter", "Filters", "Second-order passive RLC low-pass filter with Bessel-like damping for a smooth time-domain response. Input and output test points verify the DC passband.", "1 V source, series inductor, shunt capacitor and load, input/output probes")
        };

        foreach (var example in additionalExamples)
        {
            _examples.Add(new CircuitExampleItem
            {
                Id = example.Id,
                Title = example.Title,
                Category = example.Category,
                Icon = "⚡",
                Badge = "NEW",
                Description = example.Description,
                ComponentSummary = example.ComponentSummary,
                ResourcePath = $"Examples/{example.Id}.eee"
            });
        }
    }
}
