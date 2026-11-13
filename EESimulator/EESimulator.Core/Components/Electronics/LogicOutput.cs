using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Digital Logic Output (Probe) component.
/// Senses circuit node voltage and evaluates whether it represents Logic 1 or Logic 0
/// based on a customizable logic switching threshold.
/// </summary>
public class LogicOutputComponent : ElectronicComponent
{
    public const string StableType = "electronics.logicoutput";

    public override string StableTypeId => StableType;
    public override string ComponentType => "LogicOutput";
    public override string Description => "Digital Logic Output Probe (0/1)";

    /// <summary>
    /// Voltage threshold above which the probe evaluates as Logic 1 (High / ON).
    /// Default is 2.5 V (user-configurable, e.g. 1.65V for 3.3V logic).
    /// </summary>
    public double Threshold
    {
        get => ElectricalParameters.TryGetValue("Threshold", out var val) ? val : 2.5;
        set
        {
            ElectricalParameters["Threshold"] = value;
            NotifyModified(nameof(Threshold));
        }
    }

    /// <summary>
    /// Custom Logic 1 display value or scale factor (default 1.0).
    /// </summary>
    public double HighValue
    {
        get => ElectricalParameters.TryGetValue("HighValue", out var val) ? val : 1.0;
        set => ElectricalParameters["HighValue"] = value;
    }

    /// <summary>
    /// Custom Logic 0 display value or scale factor (default 0.0).
    /// </summary>
    public double LowValue
    {
        get => ElectricalParameters.TryGetValue("LowValue", out var val) ? val : 0.0;
        set => ElectricalParameters["LowValue"] = value;
    }

    /// <summary>
    /// Evaluates whether the probe is currently ON (Logic 1).
    /// </summary>
    public bool IsOn
        => LiveVoltage >= Threshold;

    public LogicOutputComponent()
    {
        Pins.Add(new ComponentPin("in", "D", -20, 0));
        Threshold = 2.5;
        HighValue = 1.0;
        LowValue = 0.0;
    }

    public LogicOutputComponent(string name, double threshold = 2.5) : this()
    {
        Name = name;
        Reference = name;
        Threshold = threshold;
    }
}
