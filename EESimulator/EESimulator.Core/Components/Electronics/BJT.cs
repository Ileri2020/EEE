using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

public enum BjtPolarity
{
    NPN,
    PNP
}

/// <summary>
/// Bipolar Junction Transistor (NPN / PNP).
/// </summary>
public class BJTComponent : ElectronicComponent
{
    public const string StableType = "electronics.bjt";

    public override string StableTypeId => StableType;
    public override string ComponentType => "BJT";
    public override string Description => "Bipolar Junction Transistor";

    public BjtPolarity Polarity { get; set; } = BjtPolarity.NPN;

    public double GainBeta
    {
        get => ElectricalParameters.TryGetValue("Bf", out var val) ? val : 100.0;
        set => ElectricalParameters["Bf"] = value;
    }

    public BJTComponent()
    {
        // Pin layout: Base (left), Collector (top-right), Emitter (bottom-right)
        Pins.Add(new ComponentPin("base", "B", -20, 0));
        Pins.Add(new ComponentPin("collector", "C", 15, -20));
        Pins.Add(new ComponentPin("emitter", "E", 15, 20));
        GainBeta = 100.0;
        PartNumber = "2N2222";
    }

    public BJTComponent(string name, BjtPolarity polarity = BjtPolarity.NPN) : this()
    {
        Name = name;
        Reference = name;
        Polarity = polarity;
    }
}
