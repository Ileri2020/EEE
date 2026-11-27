using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public sealed class RecommendedExampleScript
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedAtLabel => CreatedAt.ToLocalTime().ToString("g");
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

        return await Task.Run(() =>
        {
            CircuitNode circuit = EeePlatformSerializer.LoadFromEeeJson(json);
            EnsureLogicalPlacements(circuit);
            foreach (LogicalGridPlacement placement in LogicalGridSchematicLayout.Calculate(circuit).Placements)
            {
                placement.Component.X = placement.X;
                placement.Component.Y = placement.Y;
            }
            return circuit;
        });
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
            Title = "Complex Linked Circuit - Zonal Component & Validation Showcase",
            Category = "Digital Logic",
            Icon = "🧩",
            Badge = "FULL SHOWCASE",
            Description = "An interactive 16-zone circuit based on ComplexLinkedCircuitTests. It includes a synchronous three-phase generator, native two-/three-/single-phase transmission lines, an induction motor, household AC loads and transformer, solar and battery sources, a diode bridge, working relay and semiconductor paths, connected arithmetic displays, TTL output indicators, and measured validation points.",
            ComponentSummary = "380 components · 77 component types · 16 functional zones · 43 voltage checkpoints",
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
            ("eight_bit_cpu", "Eight-Bit CPU", "Digital Logic", "Synchronous eight-bit CPU and accumulator datapath architecture featuring an instruction/sequence counter (program counter), an 8-bit accumulator register, an 8-bit arithmetic adder ALU, interactive operand bus inputs, carry status flag, and dual hexadecimal output displays.", "8-bit operand bus, clock, 8-bit counter, 8-bit accumulator register, 8-bit adder, dual hex displays, carry flag, and probes"),
            ("three_phase_transformer", "Three-Phase Transformer", "Electrical Machines", "Three-phase Y-Y power transformer circuit supplied by balanced 400V line-to-line (230.9V RMS phase, 326.6V peak, 50 Hz) three-phase AC sources, demonstrating a 1000:575 turns ratio step-down to 230V line-to-line (187.8V peak phase) across balanced 1 kΩ secondary phase loads.", "Three balanced 400V 50 Hz AC sources, 3-phase Y-Y transformer, balanced phase loads, and peak voltage probes"),
            ("three_phase_synchronous_generator", "Three-Phase Synchronous Generator", "Passive Circuits", "Synchronous generator model with three phase terminals, neutral reference, resistive phase loads, and phase-voltage test point.", "Synchronous generator, neutral, phase loads, and test point"),
            ("three_phase_induction_motor", "Three-Phase Induction Motor", "Passive Circuits", "Three-phase induction motor equivalent model with balanced phase supplies, neutral return, and phase test point.", "Three-phase supply, induction motor model, neutral, and test point"),
            ("star_delta_motor_starter", "Star-Delta Motor Starter", "Passive Circuits", "Star-delta starter teaching diagram with an induction motor, three-phase supply, and interactive start input. The contactor interlock sequence is represented conceptually, not as a switching transient.", "Three-phase supply, induction motor, start toggle, status indicator, and test point"),
            ("complete_solar_power_system", "Complete Solar Power System", "Passive Circuits", "Solar panel, battery storage, DC load, and output test point demonstrate a small stand-alone solar system. Irradiance and battery state of charge can be edited; charge protection is not included.", "Solar panel, battery, load, status LED, and test point"),
            ("generation_transmission_network", "Electrical Generation and Transmission Network", "Passive Circuits", "End-to-end power-system study with a synchronous generator, three-phase transmission line, transformer, and balanced phase loads. Test points expose generator and load phase nodes.", "Synchronous generator, transmission line, transformer, balanced loads, and test points"),
            ("crystal_oscillator", "Crystal Oscillator", "Oscillators", "Self-sustaining 16 kHz quartz crystal oscillator using an operational amplifier (LM741, ±12V rails) with series motional RLC crystal resonance (Lm = 10 mH, Cm = 10 nF, Rm = 25 Ω), shunt case capacitance (C0 = 100 pF), back-to-back diode amplitude stabilization limiter, and 1 kΩ output load.", "LM741 op-amp, ±12V supplies, quartz crystal RLC model, shunt C0, diode limiter, 1 kΩ load, and probes"),
            ("555_timer_monostable", "555 Timer Monostable", "Oscillators", "Precision monostable one-shot multivibrator timing circuit with a fast negative-going trigger input, operational timing comparator (LM741, ±12V rails), reference divider threshold (6V), 51 ms timing charge network (R = 47 kΩ, C = 1 µF), rapid discharge diode, and 1 kΩ output load with pulse indicator LED.", "LM741 op-amp, trigger differentiator, reference divider, 47 kΩ / 1 µF timing network, discharge diode, load, and LED"),
            ("astable_multivibrator", "Astable Multivibrator", "Oscillators", "Self-oscillating two-transistor (2N2222 NPN) cross-coupled astable multivibrator producing complementary 15 Hz square waves from a single +5V DC power supply, featuring RC base timing networks (47 kΩ / 1 µF), collector load resistors (1 kΩ), and alternating indicator LEDs.", "Two 2N2222 NPN BJTs, +5V DC supply, cross-coupled 47 kΩ / 1 µF timing network, 1 kΩ collector loads, dual LEDs, and probes"),
            ("voltage_controlled_oscillator", "Voltage-Controlled Oscillator (VCO)", "Oscillators", "Precision dual op-amp voltage-controlled oscillator (VCO) where a DC control voltage (1V to 5V) directly sets the charging slope of an active Miller integrator (LM741, C = 100 nF), coupled to a hysteresis Schmitt trigger comparator (LM741) in a self-oscillating loop delivering linear triangle and square wave outputs.", "Dual LM741 op-amps, DC control source, Miller integrator, Schmitt trigger, 1 kΩ load, and probes"),
            ("sepic_converter", "SEPIC Converter", "Power Electronics", "Non-inverting Single-Ended Primary-Inductor Converter (SEPIC) stepping a 12V DC input to a regulated 11.4V DC output featuring a 50 kHz NMOS power switch, primary storage inductor (1 mH), flying energy transfer capacitor (10 µF), secondary shunt inductor (1 mH), output Schottky rectifier, and shunt capacitive filter with status LED.", "12V DC source, 50 kHz PWM clock, NMOS switch, two 1 mH inductors, 10 µF flying capacitor, Schottky diode, shunt cap, and LED"),
            ("forward_converter", "Forward Converter", "Power Electronics", "Isolated step-down forward DC-DC converter stepping 24V DC input to a regulated 5.0V DC output featuring a 50 kHz primary NMOS power switch (IRF540N), core demagnetizing reset clamp diode, 2:1 high-frequency isolation transformer, secondary forward and freewheeling diodes, LC buck output filter (L = 470 µH, C = 100 µF), and load with status LED.", "24V DC rail, 50 kHz PWM clock, NMOS switch, reset diode, 2:1 transformer, forward/freewheel diodes, LC filter, and load"),
            ("push_pull_converter", "Push-Pull Converter", "Power Electronics", "Isolated push-pull DC-DC converter stepping down a 24V DC rail to a regulated 9.5V DC output, featuring dual low-side NMOS power switches driven alternately at 50 kHz (D = 0.42), center-tapped isolation transformer with primary center tap tied to VIN, fast full-wave secondary Schottky rectification, buck LC output filter (150 µH inductor, 100 µF shunt capacitor), and 10 Ω load with status indicator.", "24V DC rail, dual 50 kHz pulse drives, low-side NMOS switches, center-tapped coupled transformer, rectifier, LC filter, load, and probes"),
            ("full_bridge_smps", "Full-Bridge SMPS", "Power Electronics", "High-efficiency isolated full-bridge (H-bridge) switch-mode power supply (SMPS) converting 24V DC to a regulated 10.8V DC output, featuring four NMOS power MOSFETs driven diagonally at 50 kHz, 2:1 high-frequency step-down isolation transformer, fast secondary full-bridge Schottky diode rectifier, buck LC filter (100 µH inductor, 220 µF shunt capacitor), and 10 Ω load with status indicator.", "24V DC rail, diagonal 50 kHz PWM drivers, 4 NMOS H-bridge, 2:1 HF transformer, full-wave rectifier, buck LC filter, load, and probes"),
            ("three_phase_controlled_rectifier", "Three-Phase Controlled Rectifier", "Power Electronics", "Three-phase half-wave phase-controlled rectifier circuit with balanced AC sources (17V peak / 12V RMS, 50 Hz, 120° displacement), three NMOS power switching arms operating with phase firing delay, common star neutral return, shunt DC capacitive reservoir filter (220 µF), 24 Ω power load, and status indicator.", "Three balanced 12V 50 Hz sources, 50 Hz gate firing generators, NMOS switching arms, shunt reservoir cap, load, lamp, and probes"),
            ("power_factor_correction", "Power Factor Correction (PFC)", "Power Electronics", "Active boost power factor correction (PFC) pre-regulator converting 24V AC mains to a boosted 38V DC bus using full-wave diode rectification, a 1 mH continuous-conduction boost inductor, 50 kHz NMOS power switch, fast recovery boost diode, and shunt capacitive reservoir filter with output indicator.", "24V AC source, full-wave diode bridge, 1 mH boost inductor, NMOS switch, 50 kHz PWM, shunt capacitor, load, and LED"),
            ("llc_resonant_converter", "LLC Resonant Converter", "Power Electronics", "High-efficiency 100 kHz LLC resonant DC-DC converter featuring half-bridge switching drive, series resonant inductor (Lr = 253 µH), resonant capacitor (Cr = 10 nF), parallel magnetizing inductor (Lm = 1.25 mH), 2:1 isolation transformer, fast secondary rectification, and shunt DC output capacitor with indicator LED.", "24V DC rail, 100 kHz half-bridge drive, Lr-Cr-Lm resonant tank, 2:1 transformer, fast rectifier, shunt cap, load, and LED"),
            ("cockcroft_walton_multiplier", "Cockcroft-Walton Voltage Multiplier", "Power Electronics", "Two-stage Cockcroft-Walton cascade voltage multiplier ladder converting 10V peak AC into a 4x multiplied 37.5V DC output via sequential diode-capacitor charge-pumping stages, ground clamping, and high-impedance resistive termination.", "10V peak AC source, 4 diodes, 4 pumping/smoothing capacitors, 1 MΩ load, and voltage stage probes"),
            ("common_source_mosfet_amplifier", "Common-Source MOSFET Amplifier", "Amplifiers", "Small-signal Class-A common-source MOSFET amplifier featuring an IRF540N transistor biased in saturation (VDD = 12V, VG = 3.37V), resistive gate divider (100 kΩ / 39 kΩ), 2.2 kΩ drain load, source degeneration resistor with AC bypass capacitor, and input/output AC coupling with a 10 kΩ load.", "IRF540N NMOS transistor, 12V VDD rail, gate divider bias, source bypass, AC coupling capacitors, 10 kΩ load, and probes"),
            ("differential_amplifier", "Differential Amplifier", "Amplifiers", "Precision operational amplifier differential difference amplifier (LM741, ±15V supplies) providing balanced differential gain (Ad = 10.0, Vout = 10 * (V+ - V-)) across four matched bridge resistors (1 kΩ / 10 kΩ) with common-mode rejection and a 1 kΩ output load.", "LM741 op-amp, dual ±15V supplies, two signal inputs, matched 1 kΩ/10 kΩ resistor bridge, 1 kΩ load, and probes"),
            ("instrumentation_amplifier", "Instrumentation Amplifier", "Amplifiers", "Classic precision 3-op-amp instrumentation amplifier circuit (LM741, ±15V supplies) featuring two high-input-impedance buffer stages (U1, U2) with user-settable gain resistor RG = 220 Ω (Gain = 92.0), matched 4-resistor difference amplifier output stage (U3, 10 kΩ matched network), common-mode rejection, and 1 kΩ output load.", "Three LM741 op-amps, dual ±15V supplies, differential sensor inputs, RG gain resistor, matched difference network, load, and probes"),
            ("class_d_audio_amplifier", "Class D Audio Amplifier", "Amplifiers", "Class D audio power amplifier with 1 kHz sine audio input, 100 kHz triangle PWM comparator modulator, complementary MOSFET half-bridge power stage (12V DC rail), LC carrier demodulation filter, and 8 Ω speaker output with audio indicator.", "1 kHz sine audio source, 100 kHz triangle carrier, LM311 op-amp PWM comparator, complementary half-bridge MOSFET switches, LC filter, 8 Ω load, and probes"),
            ("active_band_pass_filter", "Active Band-Pass Filter", "Filters", "Active 1 kHz multiple-feedback (MFB) band-pass filter using an operational amplifier (LM741, ±15V supplies), providing narrow resonant band-pass filtering (Q = 2, Av = 2) with high-frequency and low-frequency roll-off, 1 kΩ load, and status indicator.", "1 kHz AC source, LM741 op-amp, dual ±15V supplies, MFB RC feedback network, load, LED, and probes"),
            ("eight_bit_alu", "Eight-Bit ALU", "Digital Logic", "Interactive 8-bit arithmetic logic datapath with toggleable 8-bit operands (A = 0xAA, B = 0x55), carry-in control, 8-bit parallel binary adder, dual hexadecimal digit result displays (HEX0/HEX1 showing 0xFF), carry flag indicator, and digital output probes.", "16 operand toggle inputs, carry input, 8-bit parallel binary adder, dual hex digit displays, carry flag output, and probes"),
            ("sixteen_by_four_ram", "16 × 4 RAM", "Digital Logic", "Interactive 16-address by four-bit RAM demonstration with toggleable address/data inputs, synchronous clock and write-enable controls, hexadecimal digit output, and logic state indicators.", "4 address toggles, 4 data toggles, 1 Hz clock, write/output enables, 16×4 RAM, hex display, and probes"),
            ("digital_calculator", "Digital Calculator", "Digital Logic", "Four-bit arithmetic unit demonstrating selectable addition and subtraction (A + B / A - B), operand toggles, multiplexer result routing, carry/borrow status, and hexadecimal display.", "4-bit operand toggles (A and B), ADD/SUB mode select, adder, subtractor, multiplexer, hex display, and probes"),
            ("digital_clock", "Digital Clock", "Digital Logic", "Cascaded 60-second digital clock timing chain with a 1 Hz clock generator, decade unit counter (0-9), base-6 tens counter (0-5), dual hexadecimal seven-segment displays, and minute overflow indicator.", "1 Hz clock, run enable, reset, BCD decade counter, base-6 tens counter, dual hex displays, and minute tick"),
            ("traffic_light_controller", "Traffic Light Controller", "Digital Logic", "Sequential traffic light controller implementing a 4-state sequence (Red -> Red+Amber -> Green -> Amber) using a synchronous 2-bit counter, decode logic, LED signal lamps, and fail-safe Red/Green exclusion interlock.", "1 Hz clock, 2-bit state counter, NOT/AND decoder gates, Red/Amber/Green lamps, interlock logic, and probes"),
            ("single_phase_induction_motor", "Single-Phase Induction Motor", "Electrical Machines", "Capacitor-start single-phase induction motor equivalent circuit demonstrating parallel main and auxiliary starting windings, a series phase-shift capacitor with centrifugal start switch, and rotor back-EMF / torque response under AC excitation.", "120V AC mains source, main stator winding, auxiliary starting winding, 80 µF start capacitor, rotor branch, and probes"),
            ("dc_shunt_motor", "DC Shunt Motor", "Electrical Machines", "DC shunt motor equivalent circuit modeling parallel armature and field branches across a common 12 V DC bus, including armature resistance/inductance, rotational counter-EMF (back-EMF), field excitation coil, and starter current limiting resistor.", "12V DC bus source, starter resistor, parallel shunt field coil, armature R-L branch, rotational back-EMF source, and probes"),
            ("dc_series_motor", "DC Series Motor", "Electrical Machines", "DC series motor equivalent circuit demonstrating series-connected field and armature windings with opposing rotational back-EMF, showing speed-dependent counter-EMF and load-dependent series current regulation.", "12V DC source, starter resistor, series field winding, series armature winding, rotational back-EMF, and probes"),
            ("single_phase_transformer", "Single-Phase Transformer", "Electrical Machines", "Step-down single-phase power transformer circuit demonstrating ideal and magnetizing mutual inductance coupling (5:1 turns ratio, 120V to 24V RMS), core loss modeling, and secondary power delivery to a resistive load with status indicator.", "120V AC mains source, 5:1 step-down transformer, secondary 24 Ω load, indicator lamp, and voltage probes"),
            ("three_phase_star_delta_connection", "Three-Phase Star-Delta Connection", "Electrical Machines", "Balanced 50 Hz three-phase system comparing Star (Y) load with common neutral node and Delta (Δ) mesh load across 12V phase / 20.8V line voltages.", "Three 12V 50 Hz sources, Star resistor bank with neutral, Delta resistor mesh, and voltage probes"),
            ("mppt_solar_charge_controller", "MPPT Solar Charge Controller", "Renewable Energy", "Switch-mode buck solar charge controller stepping down 18V PV input to a regulated 12.6V battery charging output at 50 kHz PWM, with input buffer, freewheeling diode, LC filter, and charging indicator LED.", "18V PV source, 50 kHz PWM drive, buck power switch, Schottky freewheel diode, 1 mH inductor, 100 µF cap, load, and LED"),
            ("automatic_generator_changeover", "Automatic Generator Changeover", "Industrial Control", "Automatic transfer switch (ATS) with utility mains and standby generator supplies, interlocked transfer switching logic, load resistor, and green status LED indicator.", "Utility and generator DC supplies, interlocked transfer switches, sensing and NOT control logic, load, LED, and probes"),
            ("wien_bridge_function_generator", "Wien Bridge Function Generator", "Oscillators", "Self-sustaining 1.59 kHz Wien bridge audio oscillator using an op-amp with balanced RC positive feedback (R=10 kΩ, C=10 nF) and back-to-back diode amplitude stabilization limiter.", "LM741 op-amp, dual ±15V supplies, Wien bridge RC network, 1N4148 diode limiter, load, and probes"),
            ("sawtooth_wave_generator", "Sawtooth Wave Generator", "Oscillators", "Active 500 Hz linear asymmetric sawtooth ramp generator with op-amp high-impedance buffer follower, 1 kΩ load, and LED indicator.", "500 Hz sawtooth ramp source, LM741 active buffer follower, dual ±15V supplies, load, LED, and probes"),
            ("triangle_wave_generator", "Triangle Wave Generator", "Oscillators", "Active op-amp integrator circuit driven by a 500 Hz symmetrical square wave source, producing a true linear triangle wave across a 1 kΩ load with an LED indicator.", "500 Hz square wave source, LM741 op-amp integrator, 100 nF feedback capacitor, 100 kΩ DC bleed, load, LED, and probes"),
            ("pll_frequency_synthesizer", "PLL Frequency Synthesizer", "Oscillators", "Phase-Locked Loop frequency synthesizer featuring a 1 kHz reference clock, XOR digital phase detector, 2nd-order RC loop filter with damping, 2 kHz synthesized VCO output, D-type flip-flop feedback frequency divider (÷2), and lock indicator.", "1 kHz reference clock, XOR phase detector, 2nd-order RC loop filter, 2 kHz VCO, D-FF divider, lock LED, and probes"),
            ("single_phase_controlled_bridge_rectifier", "Single-Phase Controlled Bridge Rectifier", "Power Electronics", "Phase-controlled single-phase bridge rectifier featuring an AC mains source (12V AC, 50 Hz), gate firing pulse generator with selectable phase delay (α = 45°), controllable thyristor switch arms, free-wheeling return diodes, capacitive reservoir filter, 100 Ω load resistor, and DC output indicator.", "12V AC 50 Hz mains, gate pulse generator, thyristor switch arms, return diodes, reservoir capacitor, load, and LED"),
            ("three_phase_half_wave_rectifier", "Three-Phase Half-Wave Rectifier", "Power Electronics", "Three-phase half-wave diode rectifier supplied by balanced three-phase AC sources (12V peak, 50 Hz, 120° displacement) with star neutral return, DC reservoir smoothing capacitor, 100 Ω power load, and DC output indicator.", "Three balanced 12V 50 Hz sources, three half-wave rectifier diodes, star neutral return, reservoir capacitor, load, and LED"),
            ("three_phase_ac_voltage_controller", "Three-Phase AC Voltage Controller", "Power Electronics", "Phase-controlled three-phase AC voltage controller with balanced 12V 50 Hz sources, common star neutral return, thyristor switch arms with firing-angle control (α = 60°), RC snubbers, star-connected resistive load bank, and status indicator.", "Three balanced 12V 50 Hz sources, gate firing generator, thyristor switch arms, RC snubbers, star load bank, and probes"),
            ("single_phase_cycloconverter", "Single-Phase Cycloconverter", "Power Electronics", "Single-phase step-down cycloconverter synthesizing a 10 Hz AC output directly from a 50 Hz AC input supply using complementary 10 Hz commutation switching, an LC low-pass reconstruction filter, 100 Ω load, and status indicator.", "50 Hz AC input sources, 10 Hz commutation generator, inverter, power switches, LC demodulation filter, load, and LED"),
            ("synchronous_buck_converter", "Synchronous Buck Converter", "Power Electronics", "Synchronous step-down buck converter with a 12V DC input, 20 kHz PWM controller (40% duty cycle), complementary high-side and low-side MOSFET switches, LC output filter (1 mH inductor, 100 µF shunt capacitor), stepping down to a regulated 4.8V DC output across load and status indicator.", "12V DC source, 20 kHz PWM generator, complementary MOSFET switches, LC filter (shunt cap), load, and LED"),
            ("zeta_converter", "Zeta DC-DC Converter", "Power Electronics", "Zeta DC-DC buck-boost converter stepping a 12V DC input to a non-inverting 8.0V DC output (D = 0.4, 20 kHz PWM) featuring high-side switching transistor, primary shunt inductor, flying transfer capacitor, freewheeling clamp diode, secondary filter inductor, shunt output capacitor, load, and status indicator.", "12V DC source, 20 kHz PWM driver, switching transistor, L1 shunt inductor, flying capacitor, freewheeling diode, L2 filter inductor, shunt output cap, load, and LED"),
            ("dc_voltage_doubler", "DC Voltage Doubler", "Power Electronics", "Classic Greinacher half-wave voltage doubler with clamp diode, pump capacitor, rectifier diode, shunt reservoir capacitor, load, and LED indicator.", "5 V AC source, clamp and rectifier diodes, pump and reservoir capacitors, load, and LED"),
            ("active_clamp_flyback_converter", "Active Clamp Flyback Converter", "Power Electronics", "Isolated flyback converter with active clamp branch recycling transformer leakage energy, main switch, coupled secondary, output rectifier, shunt filter capacitor, and LED status indicator.", "12 V DC source, primary and secondary coupled inductors, active clamp switch and cap, main switch, rectifier, shunt filter, and LED"),
            ("class_a_power_amplifier", "Class A Power Amplifier", "Amplifiers", "Common-emitter Class A audio amplifier with voltage-divider bias (RB1/RB2), emitter degeneration and AC bypass capacitor (CE), AC input/output coupling capacitors (CIN/COUT), and load resistor.", "NPN transistor, divider bias, emitter bypass cap, AC coupling caps, 1 kΩ load, and probes"),
            ("class_b_push_pull_amplifier", "Class B Push-Pull Amplifier", "Amplifiers", "Complementary NPN/PNP (2N2222/2N2907) push-pull output stage with dual ±5V rails, diode bias stack eliminating crossover distortion, emitter degeneration, and AC-coupled drive.", "Complementary NPN/PNP pair, dual ±5V supplies, diode bias network, emitter resistors, load, and probes"),
            ("class_c_rf_amplifier", "Class C RF Amplifier", "Amplifiers", "High-efficiency tuned Class C RF power amplifier with 100 kHz resonant LC tank (L=253.3 µH, C=10 nF), base self-biasing, AC input/output coupling, and 1 kΩ load.", "NPN transistor, 100 kHz resonant LC tank, base self-bias resistor, AC coupling, load, and probes"),
            ("schmitt_trigger", "Schmitt Trigger", "Analog Electronics", "Non-inverting op-amp Schmitt trigger with positive feedback hysteresis (Vth = ±3.2V), sinusoidal input, bistable square-wave output, and LED state indicator.", "LM741 op-amp, positive feedback resistor, input summing resistor, load, LED indicator, and probes"),
            ("precision_rectifier", "Precision Rectifier", "Analog Electronics", "Active op-amp precision half-wave rectifier (super-diode) eliminating standard diode forward drop, with 1N4148 feedback diode, load resistor, and green LED peak indicator.", "LM741 op-amp, 1N4148 diode, load resistor, indicator LED, and probes"),
            ("active_notch_filter", "Active Notch Filter", "Filters", "Active 1 kHz Twin-T band-stop (notch) filter with op-amp buffer, showing deep rejection at the resonant notch frequency.", "1 kHz AC source, Twin-T RC network (R=15.9 kΩ, C=10 nF), LM741 buffer op-amp, load, and probes"),
            ("eight_bit_register_bank", "Eight-Bit Register Bank", "Digital Logic", "Two-register 8-bit bank with shared 8-bit data bus, 1-bit address decoding (ADDR), gated write enables, master clear, and dual 8-segment LED bar displays.", "8-bit data inputs, address decoder, clock, clear, two 8-bit registers, and dual 8-LED bars"),
            ("eight_bit_shift_register", "Eight-Bit Shift Register", "Digital Logic", "Serial-in / parallel-out (SIPO) eight-bit shift register with toggleable serial data, enable, clear, 1 Hz clock, 8-segment LED bar output, and serial input/output test probes.", "Serial input, clock, enable, clear, 8-bit shift register, 8-LED bar, and probes"),
            ("eight_bit_binary_counter", "Eight-Bit Binary Counter", "Digital Logic", "Synchronous 8-bit binary up/down counter with interactive clock, enable, direction, reset controls, dual hexadecimal digit displays (HEX0/HEX1), carry output, and bit probes.", "1 Hz clock, enable, direction, reset, 8-bit counter, dual hex displays, carry flag, and probes"),
            ("seven_segment_display_decoder", "Seven-Segment Display Decoder", "Digital Logic", "Four-bit interactive BCD/hexadecimal input driving a built-in seven-segment decoder display, with individual bit monitoring probes for BCD0 through BCD3.", "Four BCD toggle inputs, hexadecimal/7-segment decoder display, and probes"),
            ("digital_stopwatch", "Digital Stopwatch", "Digital Logic", "Two-digit 00-59 digital seconds stopwatch using cascaded BCD decade counters, 1 Hz clock generator, interactive RUN and RESET controls, and dual hexadecimal digit displays.", "1 Hz clock, RUN toggle, RESET toggle, two cascaded BCD counters (0-9, 0-5), and dual hex displays"),
            ("simple_microcontroller_system", "Simple Microcontroller System", "Digital Systems", "Educational microcontroller datapath combining a clock, small RAM, register, adder, toggleable input port, and visible output display. Instruction execution is represented as connected functional blocks.", "Clock, 4×4 RAM, 8-bit register/adder blocks, I/O toggles, and display"),
            ("permanent_magnet_dc_motor_controller", "PMDC Motor Controller", "Electrical Machines", "Toggle-controlled PMDC armature model with configurable resistance, inductance, fixed-speed back EMF, load torque, current, and terminal probes. Mechanical speed dynamics are not modeled.", "DC source, enable switch, PMDC motor, load, and probes"),
            ("brushless_dc_motor_drive", "Brushless DC Motor Drive", "Electrical Machines", "Averaged three-phase inverter drives a fixed-speed BLDC model with trapezoidal phase back EMF, configurable frequency, modulation, enable, phase currents, and load probes. Switching and mechanical dynamics are not modeled.", "DC bus, enable input, averaged inverter, BLDC motor, and probes"),
            ("variable_frequency_drive", "Variable Frequency Drive (VFD)", "Electrical Machines", "Averaged three-phase inverter with adjustable output frequency and modulation index supplying a fixed-speed induction-motor equivalent. PWM switching and mechanical speed dynamics are not modeled.", "DC bus, enable input, averaged VFD, induction motor, and probes"),
            ("single_phase_ac_motor_capacitor_starter", "Single-Phase AC Motor Capacitor Starter", "Electrical Machines", "Toggle-controlled fixed-speed single-phase motor equivalent with separate main and capacitor-start auxiliary winding branches, load, and terminal probes. Starting-switch and rotor dynamics are not modeled.", "AC source, start switch, dual-winding motor with capacitor, load, and probes"),
            ("grid_tied_solar_inverter", "Grid-Tied Solar Inverter", "Renewable Energy", "Low-voltage grid-synchronization concept with solar-source equivalent, inverter-output source, grid reference, load, and synchronism probes. This is not a grid-interconnection design.", "Solar/DC source, inverter equivalent, AC reference, load, and probes"),
            ("automatic_voltage_regulator", "Automatic Voltage Regulator (AVR)", "Electrical Control", "Steady-state closed-loop generator excitation study using a synchronous-generator field winding, op-amp controller, feedback network, voltage reference, and probes. Rotor and controller transient dynamics are not modeled.", "Synchronous generator with field winding, op-amp AVR, feedback resistors, reference, and probes"),
            ("butterworth_filter", "Butterworth Low-Pass Filter", "Filters", "Second-order Butterworth LC low-pass filter (L=2 H, C=1 µF, RL=1 kΩ, f0≈112.5 Hz, Q=0.707) with sine waveform source and maximally flat passband.", "1 V sine source, 2 H inductor, 1 µF capacitor, 1 kΩ load, and probes"),
            ("chebyshev_filter", "Chebyshev Low-Pass Filter", "Filters", "Second-order doubly-terminated Chebyshev LC low-pass filter (RS=1 kΩ, L=1.095 H, C=1 µF, RL=1 kΩ, f0≈152 Hz, Q=0.956) with matched source termination and steep passband cutoff.", "1 V sine source, 1 kΩ source resistor, 1.095 H inductor, 1 µF capacitor, 1 kΩ load, and probes"),
            ("bessel_filter", "Bessel Low-Pass Filter", "Filters", "Second-order Bessel LC low-pass filter (L=3.003 H, C=1 µF, RL=1 kΩ, f0≈91.8 Hz, Q=0.577) with linear phase and maximally flat group delay.", "1 V sine source, 3.003 H inductor, 1 µF capacitor, 1 kΩ load, and probes"),
            ("inverting_amplifier", "Inverting Amplifier", "Amplifiers", "Classic inverting operational amplifier stage with dual split DC supply (+15V, -15V, middle GND reference), input resistor Rin = 10 kΩ, feedback resistor Rf = 20 kΩ, closed-loop gain Av = -2.0, virtual ground summing junction, and 10 kΩ load.", "LM741 op-amp, dual ±15V supplies, input resistor, feedback resistor, 10 kΩ load, and probes"),
            ("non_inverting_amplifier", "Non-Inverting Amplifier", "Amplifiers", "Non-inverting operational amplifier stage with dual split DC supplies (+15V, -15V, middle GND reference), input signal applied directly to (+) input, resistor divider feedback (R1 = 10 kΩ, R2 = 20 kΩ) on (-) input, closed-loop gain Av = 1 + R2/R1 = 3.0, and 10 kΩ load.", "LM741 op-amp, dual ±15V supplies, direct input, feedback divider, 10 kΩ load, and probes"),
            ("voltage_follower", "Voltage Follower", "Amplifiers", "Unity-gain voltage follower (buffer) amplifier with dual split DC supply (+15V, -15V, middle GND reference), direct feedback connection from output to (-) input (Av = 1.0, Vout = Vin = 2.5V), high input impedance preventing source loading, and low output impedance driving 1 kΩ load.", "LM741 op-amp, dual ±15V supplies, direct feedback link, 1 kΩ load, and probes"),
            ("summing_amplifier", "Summing Amplifier", "Amplifiers", "Multi-input operational amplifier summing amplifier (audio/signal mixer) with dual split DC supply (+15V, -15V, middle GND reference), two weighted channel inputs (V1 = 1.0V, V2 = 2.0V, R1 = R2 = 10 kΩ), feedback resistor Rf = 10 kΩ producing inverted sum Vout = -(V1 + V2) = -3.0V at virtual ground summing junction.", "LM741 op-amp, dual ±15V supplies, two channel inputs, summing network, 10 kΩ load, and probes"),
            ("opamp_integrator", "Op-Amp Integrator", "Amplifiers", "Operational amplifier practical integrator circuit with dual split DC supply (+15V, -15V, middle GND reference), input resistor Rin = 10 kΩ, feedback capacitor Cf = 1.0 µF, parallel DC bleed resistor Rshunt = 100 kΩ for practical DC operating point stability (Vout = -5.0V), ramp rate dV/dt = -Vin/(Rin*Cf) = -50 V/s, and 10 kΩ load.", "LM741 op-amp, dual ±15V supplies, Rin resistor, integrating capacitor, bleed resistor, 10 kΩ load, and probes"),
            ("opamp_differentiator", "Op-Amp Differentiator", "Amplifiers", "Operational amplifier practical differentiator circuit with dual split DC supply (+15V, -15V, middle GND reference), input differentiating capacitor Cin = 100 nF, series damping resistor Rs = 100 Ω for high-frequency noise immunity, feedback resistor Rf = 10 kΩ (Vout = -Rf*Cin*dVin/dt), 0.0V DC steady state, and 10 kΩ load.", "LM741 op-amp, dual ±15V supplies, Cin capacitor, series resistor, feedback resistor, 10 kΩ load, and probes"),
            ("opamp_comparator", "Op-Amp Comparator", "Amplifiers", "Open-loop operational amplifier voltage comparator with dual split DC supply (+15V, -15V, middle GND reference), reference voltage Vref = 2.0V on inverting input (-), input signal Vin = 3.5V on non-inverting input (+), open-loop gain saturating output to positive supply rail (+15.0V), and 10 kΩ load.", "LM741 op-amp, dual ±15V supplies, Vref reference, Vin input, 10 kΩ load, and probes")
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
