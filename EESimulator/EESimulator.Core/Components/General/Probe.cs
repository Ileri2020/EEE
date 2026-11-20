using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Voltage/Current Measurement Probe Point.
/// Monitors circuit node voltage and branch current, providing live numeric readouts on canvas
/// and oscilloscope waveform recording without loading down the monitored node.
///
/// Time-variant array output:
///   When <see cref="ArrayOutputEnabled"/> is true the probe emits its captured waveform
///   as a compact array in the Python console output after each simulation run:
///     P1.in: [t=0.000s: 0.0000V, t=0.010s: 1.2340V, …]  (max <MaxReadings> entries)
///   <see cref="TimeScaleFactor"/> sub-samples the raw SpiceSharp points (1 = every point,
///   2 = every other, 10 = every 10th, etc.).
///   <see cref="TimeRangeStart"/> and <see cref="TimeRangeDuration"/> window the output to any
///   interval within the simulated span. Set Duration to 0 to include all time ≥ Start.
///   When <see cref="RealtimeArray"/> is true the output is updated after every playback tick
///   rather than only after a full manual simulation run.
/// </summary>
public class ProbeComponent : GeneralComponent
{
    public const string StableType = "general.probe";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Probe";
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

    /// <summary>Ground group used as the voltage measurement reference.</summary>
    public string ReferenceGroundGroup
    {
        get => CustomProperties.TryGetValue(nameof(ReferenceGroundGroup), out var value) &&
               !string.IsNullOrWhiteSpace(value?.ToString())
            ? value!.ToString()!.Trim()
            : GroundComponent.DefaultGroundGroup;
        set
        {
            string group = string.IsNullOrWhiteSpace(value)
                ? GroundComponent.DefaultGroundGroup
                : value.Trim();
            if (group.Equals(GroundComponent.DefaultGroundGroup, StringComparison.OrdinalIgnoreCase))
                CustomProperties.Remove(nameof(ReferenceGroundGroup));
            else
                CustomProperties[nameof(ReferenceGroundGroup)] = group;
            NotifyModified(nameof(ReferenceGroundGroup));
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

    // ── Time-variant array output properties ─────────────────────────────────

    /// <summary>
    /// When true, emits the full captured waveform as a time-indexed array in the Python
    /// console output after each simulation step. Useful for reading AC waveforms, transients,
    /// capacitor charge curves, oscillating outputs, etc.
    /// </summary>
    public bool ArrayOutputEnabled
    {
        get => CustomProperties.TryGetValue(nameof(ArrayOutputEnabled), out var v) &&
               v?.ToString()?.Equals("True", StringComparison.OrdinalIgnoreCase) == true;
        set
        {
            CustomProperties[nameof(ArrayOutputEnabled)] = value.ToString();
            NotifyModified(nameof(ArrayOutputEnabled));
        }
    }

    /// <summary>
    /// Sub-sampling factor applied to the raw SpiceSharp waveform points before building the
    /// console array. 1 = every captured point, 2 = every other point, 10 = every 10th, etc.
    /// Higher values produce shorter arrays for long simulations. Default: 1.
    /// </summary>
    public int TimeScaleFactor
    {
        get => CustomProperties.TryGetValue(nameof(TimeScaleFactor), out var v) &&
               int.TryParse(v?.ToString(), out var n) ? Math.Max(1, n) : 1;
        set
        {
            CustomProperties[nameof(TimeScaleFactor)] = Math.Max(1, value).ToString();
            NotifyModified(nameof(TimeScaleFactor));
        }
    }

    /// <summary>
    /// Start time (seconds) of the output window within the simulated span.
    /// 0 = from the very beginning of the simulation. Pairs with <see cref="TimeRangeDuration"/>.
    /// Example: set to 0.005 to skip the first 5 ms and read from there onward.
    /// </summary>
    public double TimeRangeStart
    {
        get => CustomProperties.TryGetValue(nameof(TimeRangeStart), out var v) &&
               double.TryParse(v?.ToString(), out var d) ? d : 0.0;
        set
        {
            CustomProperties[nameof(TimeRangeStart)] = Math.Max(0.0, value).ToString("G");
            NotifyModified(nameof(TimeRangeStart));
        }
    }

    /// <summary>
    /// Duration (seconds) of the output window. 0 = include all captured time ≥ TimeRangeStart.
    /// Example: set to 0.002 to output a 2 ms window starting at <see cref="TimeRangeStart"/>.
    /// </summary>
    public double TimeRangeDuration
    {
        get => CustomProperties.TryGetValue(nameof(TimeRangeDuration), out var v) &&
               double.TryParse(v?.ToString(), out var d) ? Math.Max(0.0, d) : 0.0;
        set
        {
            CustomProperties[nameof(TimeRangeDuration)] = Math.Max(0.0, value).ToString("G");
            NotifyModified(nameof(TimeRangeDuration));
        }
    }

    /// <summary>
    /// Maximum number of entries to include in the console array output.
    /// Applied after the time-window filter and sub-sampling. 0 = unlimited (capped at 500 for
    /// readability). Default: 100.
    /// </summary>
    public int MaxReadings
    {
        get => CustomProperties.TryGetValue(nameof(MaxReadings), out var v) &&
               int.TryParse(v?.ToString(), out var n) ? Math.Max(0, n) : 100;
        set
        {
            CustomProperties[nameof(MaxReadings)] = Math.Max(0, value).ToString();
            NotifyModified(nameof(MaxReadings));
        }
    }

    /// <summary>
    /// When true, the console array is refreshed after every live-playback simulation tick
    /// (the same cadence used by the schematic viewport and waveform generator), so the output
    /// grows in real-time as the simulation progresses. When false, the array is only printed
    /// after a full manual simulation run.
    /// </summary>
    public bool RealtimeArray
    {
        get => CustomProperties.TryGetValue(nameof(RealtimeArray), out var v) &&
               v?.ToString()?.Equals("True", StringComparison.OrdinalIgnoreCase) == true;
        set
        {
            CustomProperties[nameof(RealtimeArray)] = value.ToString();
            NotifyModified(nameof(RealtimeArray));
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
