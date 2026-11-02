using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Programmable Logic Array (PLA).
/// Implements configurable two-level AND-OR sum-of-products boolean logic.
/// </summary>
public class PLAComponent : ElectronicComponent
{
    public const string StableType = "electronics.pla";

    public override string StableTypeId => StableType;
    public override string ComponentType => "PLA";
    public override string SpicePrefix => "PLA";
    public override string Description => "Programmable Logic Array (configurable AND/OR matrix)";

    public int InputCount
    {
        get => ElectricalParameters.TryGetValue("InputCount", out var val) && val >= 1 ? (int)val : 4;
        set
        {
            ElectricalParameters["InputCount"] = Math.Clamp(value, 1, 8);
            RebuildPins();
            NotifyModified(nameof(InputCount));
        }
    }

    public int OutputCount
    {
        get => ElectricalParameters.TryGetValue("OutputCount", out var val) && val >= 1 ? (int)val : 4;
        set
        {
            ElectricalParameters["OutputCount"] = Math.Clamp(value, 1, 8);
            RebuildPins();
            NotifyModified(nameof(OutputCount));
        }
    }

    public int AndTerms
    {
        get => ElectricalParameters.TryGetValue("AndTerms", out var val) && val >= 1 ? (int)val : 4;
        set
        {
            ElectricalParameters["AndTerms"] = Math.Clamp(value, 1, 16);
            NotifyModified(nameof(AndTerms));
        }
    }

    public string TruthTable
    {
        get => CustomProperties.TryGetValue("TruthTable", out var val) ? val?.ToString() ?? string.Empty : string.Empty;
        set
        {
            CustomProperties["TruthTable"] = value;
            NotifyModified(nameof(TruthTable));
        }
    }

    public PLAComponent()
    {
        InputCount = 4;
        OutputCount = 4;
        AndTerms = 4;
    }

    public PLAComponent(string name, int inCount = 4, int outCount = 4, int andTerms = 4) : this()
    {
        Name = name;
        Reference = name;
        InputCount = inCount;
        OutputCount = outCount;
        AndTerms = andTerms;
    }

    private void RebuildPins()
    {
        int inputs = InputCount, outputs = OutputCount;
        var pins = new System.Collections.Generic.List<ComponentPin>();
        for (int i = 0; i < inputs; i++) pins.Add(new ComponentPin($"in{i}", $"I{i}", -24, DigitalPinLayout.Position(i, inputs)));
        for (int i = 0; i < outputs; i++) pins.Add(new ComponentPin($"out{i}", $"O{i}", 24, DigitalPinLayout.Position(i, outputs)));
        DigitalPinLayout.Replace(this, pins);
    }
}
