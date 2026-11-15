using System;

namespace EESimulator.Core.Components.Electrical;

/// <summary>A single-phase source with RMS sine voltage and configurable transient waveform.</summary>
public sealed class ACSourceComponent : VoltageSourceComponent
{
    public new const string StableType = "electrical.acsource";
    private const double SqrtTwo = 1.4142135623730951;

    public override string StableTypeId => StableType;
    public override string ComponentType => "ACSource";
    public override string Description => "Single-phase AC voltage source with configurable waveform";

    /// <summary>Source voltage in RMS volts for sine waves and pulse amplitude for pulse waves.</summary>
    public new double Voltage
    {
        get => WaveformType.ToUpperInvariant() switch
        {
            "SINE" => SineAmplitude / SqrtTwo,
            "PULSE" => PulsePulsed - PulseInitial,
            _ => base.Voltage
        };
        set
        {
            EnsureFinite(value, nameof(value));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "AC source voltage must be non-negative.");

            switch (WaveformType.ToUpperInvariant())
            {
                case "SINE":
                    double peakVoltage = value * SqrtTwo;
                    EnsureFinite(peakVoltage, nameof(value));
                    SineAmplitude = peakVoltage;
                    break;
                case "PULSE":
                    double pulseHigh = PulseInitial + value;
                    EnsureFinite(pulseHigh, nameof(value));
                    PulsePulsed = pulseHigh;
                    break;
                default:
                    base.Voltage = value;
                    break;
            }
        }
    }

    public double Frequency
    {
        get => WaveformType.ToUpperInvariant() switch
        {
            "PULSE" when PulsePeriod > 0 => 1 / PulsePeriod,
            "AM" => AmCarrierFrequency,
            "SFFM" => SffmCarrierFrequency,
            _ => SineFrequency
        };
        set
        {
            EnsureFinite(value, nameof(value));
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "AC source frequency must be greater than zero.");

            double period = 1 / value;
            if (!double.IsFinite(period) || period <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "AC source frequency is outside the supported numeric range.");

            SineFrequency = value;
            PulsePeriod = period;
            PulseWidth = PulsePeriod / 2;
            AmCarrierFrequency = value;
            SffmCarrierFrequency = value;
        }
    }

    public double PhaseDegrees
    {
        get => SinePhase;
        set
        {
            EnsureFinite(value, nameof(value));
            SinePhase = value;
        }
    }

    /// <summary>DC level used by the selected waveform.</summary>
    public double Offset
    {
        get => WaveformType.ToUpperInvariant() switch
        {
            "SINE" => SineOffset,
            "PULSE" => PulseInitial,
            _ => base.Voltage
        };
        set
        {
            EnsureFinite(value, nameof(value));
            switch (WaveformType.ToUpperInvariant())
            {
                case "SINE":
                    base.Voltage = value;
                    SineOffset = value;
                    break;
                case "PULSE":
                    double pulseAmplitude = PulsePulsed - PulseInitial;
                    double pulseHigh = value + pulseAmplitude;
                    EnsureFinite(pulseHigh, nameof(value));
                    base.Voltage = value;
                    PulseInitial = value;
                    PulsePulsed = pulseHigh;
                    break;
                default:
                    base.Voltage = value;
                    break;
            }
        }
    }

    public ACSourceComponent()
    {
        base.Voltage = 0;
        WaveformType = "Sine";
        SineOffset = 0;
        SineAmplitude = 230 * SqrtTwo;
        SineFrequency = 50;
        SinePhase = 0;
        PulseInitial = 0;
        PulsePulsed = 230;
        PulsePeriod = 1.0 / 50;
        PulseWidth = PulsePeriod / 2;
    }

    private static void EnsureFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName, "AC source values must be finite.");
    }
}
