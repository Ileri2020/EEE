using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

/// <summary>
/// Digital Clock Pulse Generator.
/// Generates periodic digital square wave pulses at a configurable frequency (Hz),
/// duty cycle (0.0 to 1.0), and High/Low voltage levels.
/// </summary>
public class ClockComponent : ElectronicComponent
{
    public const string StableType = "electronics.clock";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Clock";
    public override string Description => "Digital Clock Pulse Generator";

    /// <summary>Clock frequency in Hertz (Hz). Default is 1000 Hz (1 kHz).</summary>
    public double Frequency
    {
        get => ElectricalParameters.TryGetValue("Frequency", out var val) && val > 0 ? val : 1000.0;
        set
        {
            ElectricalParameters["Frequency"] = Math.Max(value, 0.01);
            NotifyModified(nameof(Frequency));
        }
    }

    /// <summary>Duty cycle as a fraction between 0.01 and 0.99. Default is 0.50 (50%).</summary>
    public double DutyCycle
    {
        get => ElectricalParameters.TryGetValue("DutyCycle", out var val) ? Math.Clamp(val, 0.01, 0.99) : 0.50;
        set
        {
            ElectricalParameters["DutyCycle"] = Math.Clamp(value, 0.01, 0.99);
            NotifyModified(nameof(DutyCycle));
        }
    }

    /// <summary>High logic level output voltage (V). Default 5.0 V.</summary>
    public double HighVoltage
    {
        get => ElectricalParameters.TryGetValue("HighVoltage", out var val) ? val : 5.0;
        set
        {
            ElectricalParameters["HighVoltage"] = value;
            NotifyModified(nameof(HighVoltage));
        }
    }

    /// <summary>Low logic level output voltage (V). Default 0.0 V.</summary>
    public double LowVoltage
    {
        get => ElectricalParameters.TryGetValue("LowVoltage", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["LowVoltage"] = value;
            NotifyModified(nameof(LowVoltage));
        }
    }

    /// <summary>Logisim High Duration (ticks, ATTR_HIGH). Default is 1.</summary>
    public int HighDuration
    {
        get => CustomProperties.TryGetValue("HighDuration", out var val) && int.TryParse(val?.ToString(), out var iVal) && iVal >= 1 ? iVal : 1;
        set
        {
            CustomProperties["HighDuration"] = Math.Max(value, 1);
            DutyCycle = (double)Math.Max(value, 1) / (Math.Max(value, 1) + LowDuration);
            NotifyModified(nameof(HighDuration));
        }
    }

    /// <summary>Logisim Low Duration (ticks, ATTR_LOW). Default is 1.</summary>
    public int LowDuration
    {
        get => CustomProperties.TryGetValue("LowDuration", out var val) && int.TryParse(val?.ToString(), out var iVal) && iVal >= 1 ? iVal : 1;
        set
        {
            CustomProperties["LowDuration"] = Math.Max(value, 1);
            DutyCycle = (double)HighDuration / (HighDuration + Math.Max(value, 1));
            NotifyModified(nameof(LowDuration));
        }
    }

    /// <summary>Logisim Phase Offset (ticks, ATTR_PHASE). Default is 0.</summary>
    public int Phase
    {
        get => CustomProperties.TryGetValue("Phase", out var val) && int.TryParse(val?.ToString(), out var iVal) ? iVal : 0;
        set
        {
            CustomProperties["Phase"] = Math.Max(value, 0);
            NotifyModified(nameof(Phase));
        }
    }

    /// <summary>Current instantaneous logic state: true = HIGH, false = LOW.</summary>
    public bool State
    {
        get
        {
            if (CustomProperties.TryGetValue("State", out var val) && val is bool bVal)
                return bVal;
            return LiveVoltage >= (HighVoltage + LowVoltage) / 2.0;
        }
        set
        {
            CustomProperties["State"] = value;
            LiveVoltage = value ? HighVoltage : LowVoltage;
            NotifyModified(nameof(State));
        }
    }

    public ClockComponent()
    {
        // Output terminal pin
        Pins.Add(new ComponentPin("out", "CLK", 20, 0));

        Frequency = 1000.0;
        DutyCycle = 0.50;
        HighVoltage = 5.0;
        LowVoltage = 0.0;
        ShowWaveform = true;
        ShowLiveProbe = true;
    }

    public ClockComponent(string name, double frequency = 1000.0, double highVoltage = 5.0) : this()
    {
        Name = name;
        Reference = name;
        Frequency = frequency;
        HighVoltage = highVoltage;
    }
}
