using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
///     P1.in: [t=0.000s: 0.0000V, t=0.010s: 1.2340V, …]  (max MaxReadings entries)
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

    /// <summary>
    /// Threshold in volts (or amps) above which a signal's peak-to-peak variation is considered oscillating.
    /// Default: 0.05.
    /// </summary>
    public double OscillationThreshold
    {
        get => CustomProperties.TryGetValue(nameof(OscillationThreshold), out var v) &&
               double.TryParse(v?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.05;
        set
        {
            CustomProperties[nameof(OscillationThreshold)] = value.ToString("G", CultureInfo.InvariantCulture);
            NotifyModified(nameof(OscillationThreshold));
        }
    }

    /// <summary>Discovered minimum voltage or current value across the captured waveform window.</summary>
    public double Minimum
    {
        get
        {
            var metrics = CalculateWaveformMetrics();
            if (metrics.HasData) return metrics.Min;
            if (CustomProperties.TryGetValue(nameof(Minimum), out var v) &&
                double.TryParse(v?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
            if (CustomProperties.TryGetValue("ExpectedVoltageMinimum", out var evm) &&
                double.TryParse(evm?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ed))
                return ed;
            return 0.0;
        }
        set
        {
            CustomProperties[nameof(Minimum)] = value.ToString("G", CultureInfo.InvariantCulture);
            NotifyModified(nameof(Minimum));
        }
    }

    /// <summary>Discovered maximum voltage or current value across the captured waveform window.</summary>
    public double Maximum
    {
        get
        {
            var metrics = CalculateWaveformMetrics();
            if (metrics.HasData) return metrics.Max;
            if (CustomProperties.TryGetValue(nameof(Maximum), out var v) &&
                double.TryParse(v?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
            if (CustomProperties.TryGetValue("ExpectedVoltageMaximum", out var evm) &&
                double.TryParse(evm?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ed))
                return ed;
            return 0.0;
        }
        set
        {
            CustomProperties[nameof(Maximum)] = value.ToString("G", CultureInfo.InvariantCulture);
            NotifyModified(nameof(Maximum));
        }
    }

    /// <summary>Peak-to-peak amplitude range (Maximum - Minimum).</summary>
    public double AmplitudeRange
    {
        get
        {
            var metrics = CalculateWaveformMetrics();
            if (metrics.HasData) return metrics.PeakToPeak;
            if (CustomProperties.TryGetValue(nameof(AmplitudeRange), out var v) &&
                double.TryParse(v?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
            return Math.Max(0.0, Maximum - Minimum);
        }
        set
        {
            CustomProperties[nameof(AmplitudeRange)] = value.ToString("G", CultureInfo.InvariantCulture);
            NotifyModified(nameof(AmplitudeRange));
        }
    }

    /// <summary>Peak-to-peak amplitude range alias.</summary>
    public double PeakToPeak => AmplitudeRange;

    /// <summary>
    /// Discovered fundamental oscillation frequency (in Hz) computed from cycle mean-crossings.
    /// Returns 0.0 for DC signals or non-periodic transients.
    /// </summary>
    public double Frequency
    {
        get
        {
            var metrics = CalculateWaveformMetrics();
            if (metrics.HasData) return metrics.Frequency;
            if (CustomProperties.TryGetValue(nameof(Frequency), out var v) &&
                double.TryParse(v?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
            return 0.0;
        }
        set
        {
            CustomProperties[nameof(Frequency)] = value.ToString("G", CultureInfo.InvariantCulture);
            NotifyModified(nameof(Frequency));
        }
    }

    /// <summary>
    /// Discovers whether the measured signal actively oscillates (is AC, pulse, or periodic)
    /// rather than remaining steady DC.
    /// </summary>
    public bool IsOscillating
    {
        get
        {
            var metrics = CalculateWaveformMetrics();
            if (metrics.HasData) return metrics.IsOscillating;
            if (CustomProperties.TryGetValue(nameof(IsOscillating), out var v) &&
                bool.TryParse(v?.ToString(), out var b))
                return b;
            return AmplitudeRange >= OscillationThreshold;
        }
        set
        {
            CustomProperties[nameof(IsOscillating)] = value.ToString();
            NotifyModified(nameof(IsOscillating));
        }
    }

    public record struct WaveformMetrics(
        bool HasData,
        double Min,
        double Max,
        double PeakToPeak,
        double Frequency,
        bool IsOscillating);

    public WaveformMetrics CalculateWaveformMetrics()
    {
        List<double> times;
        List<double> values;
        lock (this)
        {
            if (WaveformTime == null || WaveformTime.Count == 0)
                return new WaveformMetrics(false, 0, 0, 0, 0, false);

            times = new List<double>(WaveformTime);
            string pinId = Pins.FirstOrDefault()?.Id ?? "in";
            if (WaveformNodeVoltages.TryGetValue(pinId, out var nv) && nv.Count > 0)
                values = new List<double>(nv);
            else if (WaveformVoltage.Count > 0)
                values = new List<double>(WaveformVoltage);
            else
                return new WaveformMetrics(false, 0, 0, 0, 0, false);
        }

        if (times.Count == 0 || values.Count == 0)
            return new WaveformMetrics(false, 0, 0, 0, 0, false);

        double winStart = TimeRangeStart;
        double winEnd = TimeRangeDuration > 0 ? winStart + TimeRangeDuration : double.MaxValue;

        var filteredTimes = new List<double>();
        var filteredVals = new List<double>();
        int count = Math.Min(times.Count, values.Count);
        for (int i = 0; i < count; i++)
        {
            double t = times[i];
            if (t >= winStart && t <= winEnd)
            {
                filteredTimes.Add(t);
                filteredVals.Add(values[i]);
            }
        }

        if (filteredVals.Count == 0)
        {
            filteredTimes = times;
            filteredVals = values;
        }

        double min = double.MaxValue;
        double max = double.MinValue;
        double sum = 0.0;
        for (int i = 0; i < filteredVals.Count; i++)
        {
            double v = filteredVals[i];
            if (v < min) min = v;
            if (v > max) max = v;
            sum += v;
        }

        double vpp = Math.Max(0.0, max - min);
        double threshold = OscillationThreshold;
        if (vpp < threshold || filteredVals.Count < 3)
        {
            return new WaveformMetrics(true, min, max, vpp, 0.0, false);
        }

        double mean = sum / filteredVals.Count;
        var upwardCrossingTimes = new List<double>();
        for (int i = 1; i < filteredVals.Count; i++)
        {
            double vPrev = filteredVals[i - 1];
            double vCurr = filteredVals[i];
            if (vPrev < mean && vCurr >= mean)
            {
                double tPrev = filteredTimes[i - 1];
                double tCurr = filteredTimes[i];
                double frac = Math.Abs(vCurr - vPrev) > 1e-12 ? (mean - vPrev) / (vCurr - vPrev) : 0.0;
                upwardCrossingTimes.Add(tPrev + frac * (tCurr - tPrev));
            }
        }

        double freq = 0.0;
        bool isOsc = false;
        if (upwardCrossingTimes.Count >= 2)
        {
            double totalPeriod = upwardCrossingTimes[^1] - upwardCrossingTimes[0];
            int cycles = upwardCrossingTimes.Count - 1;
            if (totalPeriod > 0 && cycles > 0)
            {
                double avgPeriod = totalPeriod / cycles;
                freq = 1.0 / avgPeriod;
                isOsc = true;
            }
        }
        else
        {
            var downwardCrossingTimes = new List<double>();
            for (int i = 1; i < filteredVals.Count; i++)
            {
                double vPrev = filteredVals[i - 1];
                double vCurr = filteredVals[i];
                if (vPrev > mean && vCurr <= mean)
                {
                    double tPrev = filteredTimes[i - 1];
                    double tCurr = filteredTimes[i];
                    double frac = Math.Abs(vCurr - vPrev) > 1e-12 ? (mean - vPrev) / (vCurr - vPrev) : 0.0;
                    downwardCrossingTimes.Add(tPrev + frac * (tCurr - tPrev));
                }
            }

            if (downwardCrossingTimes.Count >= 2)
            {
                double totalPeriod = downwardCrossingTimes[^1] - downwardCrossingTimes[0];
                int cycles = downwardCrossingTimes.Count - 1;
                if (totalPeriod > 0 && cycles > 0)
                {
                    double avgPeriod = totalPeriod / cycles;
                    freq = 1.0 / avgPeriod;
                    isOsc = true;
                }
            }
            else
            {
                isOsc = vpp >= threshold && (max - mean > threshold / 3) && (mean - min > threshold / 3);
            }
        }

        return new WaveformMetrics(true, min, max, vpp, freq, isOsc);
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
