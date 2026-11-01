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
        return EeePlatformSerializer.LoadFromEee(tempFile);
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
    { ""Id"": ""11111111-1111-1111-1111-111111110001"", ""ComponentType"": ""VoltageSource"", ""Reference"": ""VCC"", ""Name"": ""VCC"", ""Description"": ""Main DC Power Supply (12V)"", ""X"": 100.0, ""Y"": 180.0, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 2.0, ""ElectricalParameters"": { ""Voltage"": 12.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110002"", ""ComponentType"": ""Ground"", ""Reference"": ""GND1"", ""Name"": ""GND1"", ""Description"": ""Circuit Ground Reference"", ""X"": 100.0, ""Y"": 360.0, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.0, ""ElectricalParameters"": {}, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110003"", ""ComponentType"": ""Resistor"", ""Reference"": ""R1"", ""Name"": ""R1"", ""Description"": ""Upper Base Bias Resistor (47kΩ)"", ""X"": 220.0, ""Y"": 100.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 47000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110004"", ""ComponentType"": ""Resistor"", ""Reference"": ""R2"", ""Name"": ""R2"", ""Description"": ""Lower Base Bias Resistor (10kΩ)"", ""X"": 220.0, ""Y"": 260.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110005"", ""ComponentType"": ""NPN"", ""Reference"": ""Q1"", ""Name"": ""Q1"", ""Description"": ""Common-Emitter Amplifier NPN BJT (β=150)"", ""X"": 340.0, ""Y"": 200.0, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.5, ""ElectricalParameters"": { ""Bf"": 150.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110006"", ""ComponentType"": ""Resistor"", ""Reference"": ""Rc"", ""Name"": ""Rc"", ""Description"": ""Collector Load Resistor (4.7kΩ)"", ""X"": 355.0, ""Y"": 90.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.2, ""ElectricalParameters"": { ""Resistance"": 4700.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110007"", ""ComponentType"": ""Resistor"", ""Reference"": ""Re"", ""Name"": ""Re"", ""Description"": ""Emitter Bias Stabilization Resistor (1kΩ)"", ""X"": 355.0, ""Y"": 310.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.2, ""ElectricalParameters"": { ""Resistance"": 1000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110008"", ""ComponentType"": ""Capacitor"", ""Reference"": ""Ce"", ""Name"": ""Ce"", ""Description"": ""Emitter AC Bypass Capacitor (10µF)"", ""X"": 420.0, ""Y"": 310.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.5, ""ElectricalParameters"": { ""Capacitance"": 1e-05 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110009"", ""ComponentType"": ""Capacitor"", ""Reference"": ""C1"", ""Name"": ""C1"", ""Description"": ""RC Phase Shift Network Stage 1 Capacitor (10nF)"", ""X"": 480.0, ""Y"": 140.0, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Capacitance"": 1e-08 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110010"", ""ComponentType"": ""Resistor"", ""Reference"": ""R3"", ""Name"": ""R3"", ""Description"": ""RC Phase Shift Network Stage 1 Resistor (10kΩ)"", ""X"": 540.0, ""Y"": 250.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110011"", ""ComponentType"": ""Capacitor"", ""Reference"": ""C2"", ""Name"": ""C2"", ""Description"": ""RC Phase Shift Network Stage 2 Capacitor (10nF)"", ""X"": 600.0, ""Y"": 140.0, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Capacitance"": 1e-08 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110012"", ""ComponentType"": ""Resistor"", ""Reference"": ""R4"", ""Name"": ""R4"", ""Description"": ""RC Phase Shift Network Stage 2 Resistor (10kΩ)"", ""X"": 660.0, ""Y"": 250.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110013"", ""ComponentType"": ""Capacitor"", ""Reference"": ""C3"", ""Name"": ""C3"", ""Description"": ""RC Phase Shift Network Stage 3 Capacitor (10nF)"", ""X"": 720.0, ""Y"": 140.0, ""Rotation"": 0.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Capacitance"": 1e-08 }, ""CustomProperties"": {} },
    { ""Id"": ""11111111-1111-1111-1111-111111110014"", ""ComponentType"": ""Resistor"", ""Reference"": ""R5"", ""Name"": ""R5"", ""Description"": ""RC Phase Shift Network Stage 3 Resistor (10kΩ)"", ""X"": 780.0, ""Y"": 250.0, ""Rotation"": 90.0, ""ScaleX"": 1.0, ""ScaleY"": 1.0, ""BreakingCurrent"": 0.1, ""ElectricalParameters"": { ""Resistance"": 10000.0 }, ""CustomProperties"": {} }
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
    { ""Id"": ""22222222-2222-2222-2222-222222220021"", ""FromComponentId"": ""11111111-1111-1111-1111-111111110013"", ""FromPin"": ""pin2"", ""ToComponentId"": ""11111111-1111-1111-1111-111111110005"", ""ToPin"": ""base"", ""NetName"": ""FEEDBACK_NET"", ""Resistance"": 0.0 }
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
    }
}
