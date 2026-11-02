using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// N-input Even Parity Gate.
/// Output is HIGH (1) when an even number of inputs are HIGH (0, 2, 4, ...).
/// </summary>
public class EvenParityGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.evenparity";

    public override string StableTypeId => StableType;
    public override string ComponentType => "EvenParity";
    public override string SpicePrefix => "EPAR";
    public override string Description => "N-input even-parity check gate";

    public int NumberOfInputs
    {
        get => ElectricalParameters.TryGetValue("NumberOfInputs", out var val) && val >= 2 ? (int)val : 2;
        set
        {
            ElectricalParameters["NumberOfInputs"] = Math.Clamp(value, 2, 8);
            RebuildPins();
            NotifyModified(nameof(NumberOfInputs));
        }
    }

    public EvenParityGateComponent()
    {
        NumberOfInputs = 2;
    }

    public EvenParityGateComponent(string name, int inputs = 2) : this()
    {
        Name = name;
        Reference = name;
        NumberOfInputs = inputs;
    }

    private void RebuildPins()
    {
        int count = NumberOfInputs;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int i = 0; i < count; i++) pins.Add(new ComponentPin($"in{i + 1}", ((char)('A' + i)).ToString(), -24, DigitalPinLayout.Position(i, count)));
        pins.Add(new ComponentPin("out", "Y", 24, 0));
        DigitalPinLayout.Replace(this, pins);
    }
}

/// <summary>
/// N-input Odd Parity Gate.
/// Output is HIGH (1) when an odd number of inputs are HIGH (1, 3, 5, ...).
/// Logically equivalent to a multi-input XOR gate.
/// </summary>
public class OddParityGateComponent : ElectronicComponent
{
    public const string StableType = "electronics.oddparity";

    public override string StableTypeId => StableType;
    public override string ComponentType => "OddParity";
    public override string SpicePrefix => "OPAR";
    public override string Description => "N-input odd-parity check gate";

    public int NumberOfInputs
    {
        get => ElectricalParameters.TryGetValue("NumberOfInputs", out var val) && val >= 2 ? (int)val : 2;
        set
        {
            ElectricalParameters["NumberOfInputs"] = Math.Clamp(value, 2, 8);
            RebuildPins();
            NotifyModified(nameof(NumberOfInputs));
        }
    }

    public OddParityGateComponent()
    {
        NumberOfInputs = 2;
    }

    public OddParityGateComponent(string name, int inputs = 2) : this()
    {
        Name = name;
        Reference = name;
        NumberOfInputs = inputs;
    }

    private void RebuildPins()
    {
        int count = NumberOfInputs;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int i = 0; i < count; i++) pins.Add(new ComponentPin($"in{i + 1}", ((char)('A' + i)).ToString(), -24, DigitalPinLayout.Position(i, count)));
        pins.Add(new ComponentPin("out", "Y", 24, 0));
        DigitalPinLayout.Replace(this, pins);
    }
}
