using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Voltage/Current Measurement Probe Point.
/// Monitors circuit node voltage and branch current, providing live numeric readouts on canvas
/// and oscilloscope waveform recording without loading down the monitored node.
/// </summary>
public class ProbeComponent : GeneralComponent
{
    public const string StableType = "general.probe";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Probe";
    public override string SpicePrefix => "PRB";
    public override string Description => "Voltage / Current Probe Point";

    public override bool IsSimulatable => true;

    /// <summary>Internal input resistance of the probe (default 10 MΩ to prevent loading).</summary>
    public double InputImpedance
    {
        get => ElectricalParameters.TryGetValue("InputImpedance", out var val) ? val : 10e6;
        set
        {
            ElectricalParameters["InputImpedance"] = Math.Max(value, 1e3);
            NotifyModified(nameof(InputImpedance));
        }
    }

    /// <summary>Measurement quantity: "Voltage", "Current", or "Power".</summary>
    public string MeasurementMode
    {
        get => CustomProperties.TryGetValue("MeasurementMode", out var val) ? val?.ToString() ?? "Voltage" : "Voltage";
        set
        {
            CustomProperties["MeasurementMode"] = value;
            NotifyModified(nameof(MeasurementMode));
        }
    }

    /// <summary>Display engineering units (e.g., "V", "A", "W").</summary>
    public string Unit
    {
        get => CustomProperties.TryGetValue("Unit", out var val) ? val?.ToString() ?? "V" : "V";
        set
        {
            CustomProperties["Unit"] = value;
            NotifyModified(nameof(Unit));
        }
    }

    /// <summary>Bandwidth limit in Hz (default 1 GHz).</summary>
    public double Bandwidth
    {
        get => ElectricalParameters.TryGetValue("Bandwidth", out var val) ? val : 1e9;
        set
        {
            ElectricalParameters["Bandwidth"] = Math.Max(value, 1.0);
            NotifyModified(nameof(Bandwidth));
        }
    }

    /// <summary>Logisim Display Radix (Binary, Octal, Signed Decimal, Unsigned Decimal, Hexadecimal).</summary>
    public EESimulator.Core.Components.Electronics.DisplayRadix Radix
    {
        get
        {
            if (CustomProperties.TryGetValue("Radix", out var val) && val is EESimulator.Core.Components.Electronics.DisplayRadix r)
                return r;
            if (CustomProperties.TryGetValue("Radix", out var strVal) && Enum.TryParse<EESimulator.Core.Components.Electronics.DisplayRadix>(strVal?.ToString(), true, out var parsed))
                return parsed;
            return EESimulator.Core.Components.Electronics.DisplayRadix.Binary;
        }
        set
        {
            CustomProperties["Radix"] = value;
            NotifyModified(nameof(Radix));
        }
    }

    public ProbeComponent()
    {
        Pins.Add(new ComponentPin("in", "IN", -15, 0));
        InputImpedance = 10e6;
        ShowLiveProbe = true;
        ShowWaveform = true;
    }

    public ProbeComponent(string name, double x = 0, double y = 0) : this()
    {
        Name = name;
        Reference = name;
        X = x;
        Y = y;
    }
}
