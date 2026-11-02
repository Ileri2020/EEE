using System;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>Pin-level 74-series TTL package. Pin IDs preserve the datasheet DIP pin number.</summary>
public sealed class TtlPackageComponent : ElectronicComponent
{
    public const string StableTypePrefix = "ttl.";
    private static readonly Dictionary<string, int> PackagePinCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["7400"] = 14, ["7402"] = 14, ["7404"] = 14, ["7408"] = 14, ["7410"] = 14, ["7411"] = 14,
        ["7420"] = 14, ["7421"] = 14, ["7427"] = 14, ["7430"] = 14, ["7432"] = 14, ["7434"] = 14,
        ["7436"] = 14, ["7442"] = 16, ["7443"] = 16, ["7444"] = 16, ["7447"] = 16, ["7451"] = 14,
        ["7454"] = 14, ["7458"] = 14, ["7464"] = 14, ["7474"] = 14, ["7476"] = 16, ["7485"] = 16,
        ["7486"] = 14, ["7487"] = 16, ["74138"] = 16, ["74139"] = 16, ["74151"] = 16,
        ["74153"] = 16, ["74157"] = 16, ["74161"] = 16, ["74163"] = 16, ["74165"] = 16,
        ["74166"] = 16, ["74175"] = 16, ["74191"] = 16, ["74194"] = 16, ["74195"] = 16,
        ["74240"] = 20, ["74241"] = 20, ["74244"] = 20, ["74245"] = 20, ["74266"] = 14,
        ["74273"] = 20, ["74283"] = 16, ["74299"] = 20, ["74377"] = 20, ["74381"] = 20,
        ["74541"] = 20, ["74670"] = 16, ["747266"] = 14
    };

    public new string PartNumber
    {
        get => CustomProperties.TryGetValue(nameof(PartNumber), out var value) ? value?.ToString() ?? "7400" : "7400";
        set
        {
            string number = Normalize(value);
            if (!PackagePinCounts.ContainsKey(number)) throw new ArgumentOutOfRangeException(nameof(value), $"Unsupported TTL package {value}.");
            if (string.Equals(PartNumber, number, StringComparison.OrdinalIgnoreCase))
            {
                if (Pins.Count == 0) RebuildPins();
                return;
            }

            // Simulation state and output pin values belong to the selected IC, not the package
            // instance. Do not let stale outputs from a previous part keep driving the circuit.
            foreach (string key in CustomProperties.Keys
                         .Where(key => key.StartsWith("_ttl", StringComparison.Ordinal) ||
                                       key.StartsWith("Output:pin", StringComparison.Ordinal) ||
                                       key.StartsWith("OutputVoltage:pin", StringComparison.Ordinal) ||
                                       key.StartsWith("HighZ:pin", StringComparison.Ordinal))
                         .ToArray())
            {
                CustomProperties.Remove(key);
            }
            CustomProperties[nameof(PartNumber)] = number;
            RebuildPins();
            NotifyModified(nameof(PartNumber));
        }
    }

    public override string StableTypeId => StableTypePrefix + PartNumber;
    public override string ComponentType => PartNumber;
    public override string SpicePrefix => "U";
    public override string Description => $"74-series TTL integrated circuit ({PartNumber})";
    public int PackagePinCount => PackagePinCounts[PartNumber];
    public bool ExposeSupplyPins
    {
        get => !CustomProperties.TryGetValue(nameof(ExposeSupplyPins), out var value) || Convert.ToBoolean(value);
        set { CustomProperties[nameof(ExposeSupplyPins)] = value; RebuildPins(); NotifyModified(nameof(ExposeSupplyPins)); }
    }

    public bool ShowInternalStructure
    {
        get => CustomProperties.TryGetValue(nameof(ShowInternalStructure), out var value) && Convert.ToBoolean(value);
        set { CustomProperties[nameof(ShowInternalStructure)] = value; NotifyModified(nameof(ShowInternalStructure)); }
    }

    public TtlPackageComponent() : this("7400") { }
    public TtlPackageComponent(string partNumber)
    {
        CustomProperties[nameof(PartNumber)] = "7400";
        CustomProperties[nameof(ExposeSupplyPins)] = true;
        CustomProperties[nameof(ShowInternalStructure)] = false;
        PartNumber = partNumber;
    }

    private void RebuildPins()
    {
        if (CustomProperties.TryGetValue(nameof(ExposeSupplyPins), out var expose) && !Convert.ToBoolean(expose))
        {
            var filtered = Enumerable.Range(1, PackagePinCount).Where(pin => pin != SupplyPin && pin != GroundPin).Select(CreatePin).ToArray();
            DigitalPinLayout.Replace(this, filtered);
            return;
        }
        DigitalPinLayout.Replace(this, Enumerable.Range(1, PackagePinCount).Select(CreatePin).ToArray());
    }

    private ComponentPin CreatePin(int pin)
    {
        int half = PackagePinCount / 2;
        bool left = pin <= half;
        int index = left ? pin - 1 : PackagePinCount - pin;
        string name = TtlPinCatalog.GetPinName(PartNumber, pin);
        double x = left ? -30 : 30;
        double y = DigitalPinLayout.Position(index, half, 18.0);
        return new ComponentPin($"pin{pin}", name, x, y);
    }

    public static bool IsSupported(string partNumber) => PackagePinCounts.ContainsKey(Normalize(partNumber));
    public static IReadOnlyDictionary<string, int> SupportedPackages => PackagePinCounts;
    public static int GetPackagePinCount(string partNumber) => PackagePinCounts[Normalize(partNumber)];
    public static int GetSupplyPin(string partNumber) => Normalize(partNumber) switch
    {
        "7474" => 14,
        "7476" => 5,
        _ => PackagePinCounts[Normalize(partNumber)]
    };
    public static int GetGroundPin(string partNumber) => Normalize(partNumber) switch
    {
        "7474" => 7,
        "7476" => 9,
        "74175" => 8,
        _ => PackagePinCounts[Normalize(partNumber)] / 2
    };
    public int SupplyPin => GetSupplyPin(PartNumber);
    public int GroundPin => GetGroundPin(PartNumber);
    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant().Replace("SN", string.Empty).Replace("74LS", "74").Replace("74HC", "74");
}

/// <summary>Function metadata for TTL gates, in datasheet physical pin numbering.</summary>
public static class TtlPinCatalog
{
    public readonly record struct Gate(int Output, int[] Inputs, string Operation, bool ActiveLow = false);
    private static readonly Dictionary<string, string[]> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["7400"] = new[] {"1A","1B","1Y","2A","2B","2Y","GND","3Y","3A","3B","4Y","4A","4B","VCC"},
        ["7402"] = new[] {"1Y","1A","1B","2Y","2A","2B","GND","3A","3B","3Y","4A","4B","4Y","VCC"},
        ["7404"] = new[] {"1A","1Y","2A","2Y","3A","3Y","GND","4Y","4A","5Y","5A","6Y","6A","VCC"},
        ["7408"] = new[] {"1A","1B","1Y","2A","2B","2Y","GND","3Y","3A","3B","4Y","4A","4B","VCC"},
        ["7432"] = new[] {"1A","1B","1Y","2A","2B","2Y","GND","3Y","3A","3B","4Y","4A","4B","VCC"},
        ["7486"] = new[] {"1A","1B","1Y","2A","2B","2Y","GND","3Y","3A","3B","4Y","4A","4B","VCC"},
        ["7474"] = new[] {"1CLR","1D","1CLK","1PRE","1Q","1Qn","GND","2Qn","2Q","2PRE","2CLK","2D","2CLR","VCC"},
        ["7476"] = new[] {"1CLK","1PRE","1CLR","1J","VCC","2CLK","2PRE","2CLR","GND","2J","2Qn","2Q","2K","1Qn","1Q","1K"},
        ["74138"] = new[] {"A","B","C","G2A","G2B","G1","Y7","GND","Y6","Y5","Y4","Y3","Y2","Y1","Y0","VCC"},
        ["74139"] = new[] {"1G","1A","1B","1Y0","1Y1","1Y2","1Y3","GND","2Y3","2Y2","2Y1","2Y0","2B","2A","2G","VCC"},
        ["74151"] = new[] {"D3","D2","D1","D0","Y","W","G","GND","C","B","A","D7","D6","D5","D4","VCC"},
        ["74153"] = new[] {"1B","1C3","1C2","1C1","1C0","1A","1Y","GND","2Y","2A","2C0","2C1","2C2","2C3","2B","VCC"},
        ["74157"] = new[] {"S","1A","1B","1Y","2A","2B","2Y","GND","3Y","3A","3B","4Y","4A","4B","G","VCC"},
        ["74161"] = new[] {"CLR","CLK","A","B","C","D","ENP","GND","LOAD","ENT","QD","QC","QB","QA","RCO","VCC"},
        ["74163"] = new[] {"CLR","CLK","A","B","C","D","ENP","GND","LOAD","ENT","QD","QC","QB","QA","RCO","VCC"},
        ["74165"] = new[] {"SH/LD","CLK","E","F","G","H","QHn","GND","QH","SER","D","C","B","A","CLK_INH","VCC"},
        ["74166"] = new[] {"SER","D","C","B","A","SH/LD","CLK","GND","CLK_INH","H","G","F","E","QH","QHn","VCC"},
        ["74175"] = new[] {"1Q","1Qn","1D","2D","2Qn","2Q","3D","GND","3Q","3Qn","4D","4Qn","4Q","CLK","CLR","VCC"},
        ["7442"] = new[] {"Y0","Y1","Y2","Y3","Y4","Y5","Y6","GND","Y7","Y8","Y9","D","C","B","A","VCC"},
        ["7443"] = new[] {"Y0","Y1","Y2","Y3","Y4","Y5","Y6","GND","Y7","Y8","Y9","D","C","B","A","VCC"},
        ["7444"] = new[] {"Y0","Y1","Y2","Y3","Y4","Y5","Y6","GND","Y7","Y8","Y9","D","C","B","A","VCC"},
        ["7447"] = new[] {"B","C","LT","RBI","BI/RBO","D","A","GND","e","d","c","b","a","g","f","VCC"},
        ["74381"] = new[] {"A1","B1","A0","B0","S0","S1","S2","F0","F1","GND","F2","F3","G","P","CI","B3","A3","B2","A2","VCC"},
        ["74240"] = new[] {"1OE","1A1","2Y4","1A2","2Y3","1A3","2Y2","1A4","2Y1","GND","1Y4","2A1","1Y3","2A2","1Y2","2A3","1Y1","2A4","2OE","VCC"},
        ["74241"] = new[] {"1OE","1A1","2Y4","1A2","2Y3","1A3","2Y2","1A4","2Y1","GND","1Y4","2A1","1Y3","2A2","1Y2","2A3","1Y1","2A4","2OE","VCC"},
        ["74244"] = new[] {"1OE","1A1","2Y4","1A2","2Y3","1A3","2Y2","1A4","2Y1","GND","1Y4","2A1","1Y3","2A2","1Y2","2A3","1Y1","2A4","2OE","VCC"},
        ["74541"] = new[] {"1OE","1A1","2Y4","1A2","2Y3","1A3","2Y2","1A4","2Y1","GND","1Y4","2A1","1Y3","2A2","1Y2","2A3","1Y1","2A4","2OE","VCC"},
        ["74245"] = new[] {"DIR","A1","A2","A3","A4","A5","A6","A7","A8","GND","B8","B7","B6","B5","B4","B3","B2","B1","OE","VCC"},
        ["74273"] = new[] {"CLR","Q0","D0","D1","Q1","Q2","D2","D3","Q3","GND","NC","Q4","D4","D5","Q5","Q6","D6","D7","Q7","VCC"},
        ["74377"] = new[] {"EN","Q0","D0","D1","Q1","Q2","D2","D3","Q3","GND","NC","Q4","D4","D5","Q5","Q6","D6","D7","Q7","VCC"},
        ["74283"] = new[] {"S2","B2","A2","S1","A1","B1","C0","GND","C4","S4","B4","A4","S3","A3","B3","VCC"},
        ["74670"] = new[] {"D2","D3","D4","RA1","RA0","Q4","Q3","GND","Q2","Q1","nOE","nWE","WA1","WA0","D1","VCC"}
    };

    private static readonly Dictionary<string, Gate[]> Gates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["7400"] = new[] { new Gate(3,new[]{1,2},"NAND"),new Gate(6,new[]{4,5},"NAND"),new Gate(8,new[]{9,10},"NAND"),new Gate(11,new[]{12,13},"NAND") },
        ["7402"] = new[] { new Gate(1,new[]{2,3},"NOR"),new Gate(4,new[]{5,6},"NOR"),new Gate(10,new[]{8,9},"NOR"),new Gate(13,new[]{11,12},"NOR") },
        ["7404"] = new[] { new Gate(2,new[]{1},"NOT"),new Gate(4,new[]{3},"NOT"),new Gate(6,new[]{5},"NOT"),new Gate(8,new[]{9},"NOT"),new Gate(10,new[]{11},"NOT"),new Gate(12,new[]{13},"NOT") },
        ["7408"] = new[] { new Gate(3,new[]{1,2},"AND"),new Gate(6,new[]{4,5},"AND"),new Gate(8,new[]{9,10},"AND"),new Gate(11,new[]{12,13},"AND") },
        ["7410"] = new[] { new Gate(12,new[]{1,2,13},"NAND"),new Gate(6,new[]{3,4,5},"NAND"),new Gate(8,new[]{9,10,11},"NAND") },
        ["7411"] = new[] { new Gate(12,new[]{1,2,13},"AND"),new Gate(6,new[]{3,4,5},"AND"),new Gate(8,new[]{9,10,11},"AND") },
        ["7420"] = new[] { new Gate(6,new[]{1,2,4,5},"NAND"),new Gate(8,new[]{9,10,12,13},"NAND") },
        ["7421"] = new[] { new Gate(6,new[]{1,2,4,5},"AND"),new Gate(8,new[]{9,10,12,13},"AND") },
        ["7427"] = new[] { new Gate(1,new[]{2,3,4},"NOR"),new Gate(13,new[]{5,6,12},"NOR"),new Gate(11,new[]{8,9,10},"NOR") },
        ["7430"] = new[] { new Gate(8,new[]{1,2,3,4,5,6,11,12},"NAND") },
        ["7432"] = new[] { new Gate(3,new[]{1,2},"OR"),new Gate(6,new[]{4,5},"OR"),new Gate(8,new[]{9,10},"OR"),new Gate(11,new[]{12,13},"OR") },
        ["7434"] = new[] { new Gate(2,new[]{1},"BUF"),new Gate(4,new[]{3},"BUF"),new Gate(6,new[]{5},"BUF"),new Gate(8,new[]{9},"BUF"),new Gate(10,new[]{11},"BUF"),new Gate(12,new[]{13},"BUF") },
        ["7436"] = new[] { new Gate(1,new[]{2,3},"NOR"),new Gate(4,new[]{5,6},"NOR"),new Gate(10,new[]{8,9},"NOR"),new Gate(13,new[]{11,12},"NOR") },
        ["7486"] = new[] { new Gate(3,new[]{1,2},"XOR"),new Gate(6,new[]{4,5},"XOR"),new Gate(8,new[]{9,10},"XOR"),new Gate(11,new[]{12,13},"XOR") },
        ["7451"] = new[] { new Gate(6,new[]{2,3,4,5},"AOI22"),new Gate(8,new[]{1,13,9,10},"AOI22") },
        ["7458"] = new[] { new Gate(6,new[]{2,3,4,5},"AO22"),new Gate(8,new[]{1,13,12,11,10,9},"AO32") },
        ["74138"] = Array.Empty<Gate>(),
        ["74139"] = Array.Empty<Gate>(),
        ["74151"] = Array.Empty<Gate>(),
        ["74153"] = Array.Empty<Gate>(),
        ["74157"] = Array.Empty<Gate>(),
        ["74266"] = new[] { new Gate(3,new[]{1,2},"XNOR"),new Gate(6,new[]{4,5},"XNOR"),new Gate(8,new[]{9,10},"XNOR"),new Gate(11,new[]{12,13},"XNOR") },
        ["747266"] = new[] { new Gate(3,new[]{1,2},"XNOR"),new Gate(6,new[]{4,5},"XNOR"),new Gate(8,new[]{9,10},"XNOR"),new Gate(11,new[]{12,13},"XNOR") }
    };

    public static string GetPinName(string part, int pin)
    {
        if (pin < 1 || pin > TtlPackageComponent.GetPackagePinCount(part))
            throw new ArgumentOutOfRangeException(nameof(pin), "Pin number must be within the package pin count.");
        if (Names.TryGetValue(part, out var names) && pin <= names.Length) return names[pin - 1];
        if (pin == TtlPackageComponent.GetSupplyPin(part)) return "VCC";
        if (pin == TtlPackageComponent.GetGroundPin(part)) return "GND";
        return $"P{pin}";
    }

    public static IReadOnlyList<Gate> GetGates(string part) => Gates.TryGetValue(part, out var gates) ? gates : Array.Empty<Gate>();

    public static IReadOnlyList<int> GetOutputs(string part) => part switch
    {
        "7400" or "7408" or "7432" or "7486" or "74266" or "747266" => new[] { 3, 6, 8, 11 },
        "7402" or "7436" => new[] { 1, 4, 10, 13 },
        "7404" or "7434" => new[] { 2, 4, 6, 8, 10, 12 },
        "7410" or "7411" => new[] { 6, 8, 12 },
        "7420" or "7421" => new[] { 6, 8 },
        "7427" => new[] { 1, 11, 13 },
        "7430" => new[] { 8 },
        "7442" or "7443" or "7444" => new[] { 1, 2, 3, 4, 5, 6, 7, 9, 10, 11 },
        "7447" => new[] { 9, 10, 11, 12, 13, 14, 15 },
        "7451" or "7458" => new[] { 6, 8 },
        "7454" => new[] { 8 },
        "7464" => new[] { 8 },
        "7474" => new[] { 5, 6, 8, 9 },
        "7476" => new[] { 11, 12, 14, 15 },
        "7485" => new[] { 5, 6, 7 },
        "7487" => new[] { 3, 6, 9, 12 },
        "74138" => new[] { 7, 9, 10, 11, 12, 13, 14, 15 },
        "74139" => new[] { 4, 5, 6, 7, 9, 10, 11, 12 },
        "74151" => new[] { 5, 6 },
        "74153" => new[] { 7, 9 },
        "74157" => new[] { 4, 7, 9, 12 },
        "74161" or "74163" => new[] { 11, 12, 13, 14, 15 },
        "74191" => new[] { 2, 3, 6, 7, 13, 14 },
        "74165" => new[] { 7, 9 },
        "74166" => new[] { 13, 14 },
        "74175" => new[] { 1, 2, 5, 6, 9, 10, 12, 13 },
        "74194" => new[] { 15, 14, 13, 12 },
        "74195" => new[] { 2, 3, 6, 7 },
        "74240" or "74241" or "74244" or "74541" => new[] { 2, 4, 6, 8, 11, 13, 15, 17 },
        "74245" => new[] { 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17, 18 },
        "74273" or "74377" => new[] { 2, 5, 6, 9, 12, 15, 16, 19 },
        "74283" => new[] { 1, 4, 9, 10, 13 },
        "74299" => new[] { 4, 5, 6, 7, 8, 13, 14, 15, 16, 17 },
        "74381" => new[] { 8, 9, 11, 12, 13, 14 },
        "74670" => new[] { 6, 7, 9, 10 },
        "747266" => new[] { 3, 6, 8, 11 },
        _ => Array.Empty<int>()
    };
}
