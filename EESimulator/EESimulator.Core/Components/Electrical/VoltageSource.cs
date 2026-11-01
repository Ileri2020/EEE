using System;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Independent DC/AC and transient waveform voltage source.
/// Fully mapped to SpiceSharp.Components.VoltageSource and waveforms (Sine, Pulse, Pwl, Am, Sffm).
/// </summary>
public class VoltageSourceComponent : ElectricalComponent
{
    public const string StableType = "electrical.voltagesource";

    public override string StableTypeId => StableType;
    public override string ComponentType => "VoltageSource";
    public override string SpicePrefix => "V";
    public override string Description => "Independent DC/AC and transient waveform voltage source";

    // ── DC & AC Small-Signal Properties ──────────────────────────────────────
    public double Voltage
    {
        get => ElectricalParameters.TryGetValue("Voltage", out var val) ? val : 5.0;
        set => ElectricalParameters["Voltage"] = value;
    }

    public double AcMagnitude
    {
        get => ElectricalParameters.TryGetValue("AcMagnitude", out var val) ? val : 0.0;
        set => ElectricalParameters["AcMagnitude"] = value;
    }

    public double AcPhase
    {
        get => ElectricalParameters.TryGetValue("AcPhase", out var val) ? val : 0.0;
        set => ElectricalParameters["AcPhase"] = value;
    }

    // ── Waveform Selection: "DC", "Sine", "Pulse", "Pwl", "Am", "Sffm" ─────────
    public string WaveformType
    {
        get => CustomProperties.TryGetValue("WaveformType", out var v) ? v?.ToString() ?? "DC" : "DC";
        set => CustomProperties["WaveformType"] = value;
    }

    // ── Sine Parameters ──────────────────────────────────────────────────────
    public double SineOffset
    {
        get => ElectricalParameters.TryGetValue("SineOffset", out var val) ? val : 0.0;
        set => ElectricalParameters["SineOffset"] = value;
    }
    public double SineAmplitude
    {
        get => ElectricalParameters.TryGetValue("SineAmplitude", out var val) ? val : 5.0;
        set => ElectricalParameters["SineAmplitude"] = value;
    }
    public double SineFrequency
    {
        get => ElectricalParameters.TryGetValue("SineFrequency", out var val) ? val : 1000.0;
        set => ElectricalParameters["SineFrequency"] = value;
    }
    public double SineDelay
    {
        get => ElectricalParameters.TryGetValue("SineDelay", out var val) ? val : 0.0;
        set => ElectricalParameters["SineDelay"] = value;
    }
    public double SineTheta
    {
        get => ElectricalParameters.TryGetValue("SineTheta", out var val) ? val : 0.0;
        set => ElectricalParameters["SineTheta"] = value;
    }
    public double SinePhase
    {
        get => ElectricalParameters.TryGetValue("SinePhase", out var val) ? val : 0.0;
        set => ElectricalParameters["SinePhase"] = value;
    }

    // ── Pulse Parameters ─────────────────────────────────────────────────────
    public double PulseInitial
    {
        get => ElectricalParameters.TryGetValue("PulseInitial", out var val) ? val : 0.0;
        set => ElectricalParameters["PulseInitial"] = value;
    }
    public double PulsePulsed
    {
        get => ElectricalParameters.TryGetValue("PulsePulsed", out var val) ? val : 5.0;
        set => ElectricalParameters["PulsePulsed"] = value;
    }
    public double PulseDelay
    {
        get => ElectricalParameters.TryGetValue("PulseDelay", out var val) ? val : 0.0;
        set => ElectricalParameters["PulseDelay"] = value;
    }
    public double PulseRiseTime
    {
        get => ElectricalParameters.TryGetValue("PulseRiseTime", out var val) ? val : 1e-6;
        set => ElectricalParameters["PulseRiseTime"] = value;
    }
    public double PulseFallTime
    {
        get => ElectricalParameters.TryGetValue("PulseFallTime", out var val) ? val : 1e-6;
        set => ElectricalParameters["PulseFallTime"] = value;
    }
    public double PulseWidth
    {
        get => ElectricalParameters.TryGetValue("PulseWidth", out var val) ? val : 0.5e-3;
        set => ElectricalParameters["PulseWidth"] = value;
    }
    public double PulsePeriod
    {
        get => ElectricalParameters.TryGetValue("PulsePeriod", out var val) ? val : 1e-3;
        set => ElectricalParameters["PulsePeriod"] = value;
    }

    // ── Piecewise Linear (Pwl) ───────────────────────────────────────────────
    public string PwlPoints
    {
        get => CustomProperties.TryGetValue("PwlPoints", out var v) ? v?.ToString() ?? "0,0; 1e-3,5; 2e-3,0" : "0,0; 1e-3,5; 2e-3,0";
        set => CustomProperties["PwlPoints"] = value;
    }

    // ── Amplitude Modulation (Am) ────────────────────────────────────────────
    public double AmAmplitude
    {
        get => ElectricalParameters.TryGetValue("AmAmplitude", out var val) ? val : 5.0;
        set => ElectricalParameters["AmAmplitude"] = value;
    }
    public double AmOffset
    {
        get => ElectricalParameters.TryGetValue("AmOffset", out var val) ? val : 0.0;
        set => ElectricalParameters["AmOffset"] = value;
    }
    public double AmModulationFrequency
    {
        get => ElectricalParameters.TryGetValue("AmModFreq", out var val) ? val : 100.0;
        set => ElectricalParameters["AmModFreq"] = value;
    }
    public double AmCarrierFrequency
    {
        get => ElectricalParameters.TryGetValue("AmCarrierFreq", out var val) ? val : 10000.0;
        set => ElectricalParameters["AmCarrierFreq"] = value;
    }
    public double AmDelay
    {
        get => ElectricalParameters.TryGetValue("AmDelay", out var val) ? val : 0.0;
        set => ElectricalParameters["AmDelay"] = value;
    }

    // ── Single-Frequency FM (Sffm) ───────────────────────────────────────────
    public double SffmOffset
    {
        get => ElectricalParameters.TryGetValue("SffmOffset", out var val) ? val : 0.0;
        set => ElectricalParameters["SffmOffset"] = value;
    }
    public double SffmAmplitude
    {
        get => ElectricalParameters.TryGetValue("SffmAmplitude", out var val) ? val : 5.0;
        set => ElectricalParameters["SffmAmplitude"] = value;
    }
    public double SffmCarrierFrequency
    {
        get => ElectricalParameters.TryGetValue("SffmCarrierFreq", out var val) ? val : 10000.0;
        set => ElectricalParameters["SffmCarrierFreq"] = value;
    }
    public double SffmModulationIndex
    {
        get => ElectricalParameters.TryGetValue("SffmModIndex", out var val) ? val : 1.0;
        set => ElectricalParameters["SffmModIndex"] = value;
    }
    public double SffmSignalFrequency
    {
        get => ElectricalParameters.TryGetValue("SffmSignalFreq", out var val) ? val : 100.0;
        set => ElectricalParameters["SffmSignalFreq"] = value;
    }

    public VoltageSourceComponent()
    {
        Pins.Add(new ComponentPin("positive", "+", 0, -20));
        Pins.Add(new ComponentPin("negative", "-", 0, 20));
        Voltage = 5.0;
        AcMagnitude = 0.0;
        AcPhase = 0.0;
        WaveformType = "DC";

        SineAmplitude = 5.0;
        SineFrequency = 1000.0;
    }

    public VoltageSourceComponent(string name, double voltage = 5.0) : this()
    {
        Name = name;
        Reference = name;
        Voltage = voltage;
    }
}
