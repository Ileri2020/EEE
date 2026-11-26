using System;
using System.IO;
using EESimulator.Core.Components.Audio;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>
/// Audio input transducer component (Microphone) that accepts an MP3 or WAV audio file,
/// restricts the signal to a user-specified bandwidth, and converts it into a SPICE voltage signal.
/// </summary>
public class MicrophoneComponent : VoltageSourceComponent
{
    public new const string StableType = "electrical.microphone";

    public override string StableTypeId => StableType;
    public override string ComponentType => "Microphone";
    public override string SpicePrefix => "MIC";
    public override string Description => "Microphone audio signal transducer with configurable bandwidth";

    private AudioDecodedData? _audioData;

    /// <summary>Path to the audio or music MP3/WAV file.</summary>
    public string AudioFilePath
    {
        get => CustomProperties.TryGetValue("AudioFilePath", out var val) ? val?.ToString() ?? string.Empty : string.Empty;
        set
        {
            CustomProperties["AudioFilePath"] = value ?? string.Empty;
            LoadAudioFile(value ?? string.Empty);
            NotifyModified(nameof(AudioFilePath));
        }
    }

    /// <summary>User-specified signal bandwidth in Hz (e.g., 20 kHz for full audio, 4 kHz for telephony/speech).</summary>
    public double SignalBandwidth
    {
        get => ElectricalParameters.TryGetValue("SignalBandwidth", out var val) ? val : 20000.0;
        set
        {
            double clamped = Math.Clamp(value, 20.0, 100000.0);
            ElectricalParameters["SignalBandwidth"] = clamped;
            SetBandwidth(clamped);
            NotifyModified(nameof(SignalBandwidth));
        }
    }

    /// <summary>Peak output signal amplitude in Volts.</summary>
    public double Amplitude
    {
        get => ElectricalParameters.TryGetValue("Amplitude", out var val) ? val : 1.0;
        set
        {
            ElectricalParameters["Amplitude"] = value;
            RebuildAudioWaveform();
            NotifyModified(nameof(Amplitude));
        }
    }

    /// <summary>DC bias voltage offset in Volts.</summary>
    public double Offset
    {
        get => ElectricalParameters.TryGetValue("Offset", out var val) ? val : 0.0;
        set
        {
            ElectricalParameters["Offset"] = value;
            base.Voltage = value;
            RebuildAudioWaveform();
            NotifyModified(nameof(Offset));
        }
    }

    /// <summary>Decoded audio data samples and metadata.</summary>
    public AudioDecodedData AudioData => _audioData ??= AudioDecoderService.GenerateAcousticSignal(1.0, 44100, SignalBandwidth);

    /// <summary>Effective audio sample rate in Hz.</summary>
    public int SampleRate => AudioData.SampleRate;

    /// <summary>Duration of the loaded audio signal in seconds.</summary>
    public double DurationSeconds => AudioData.DurationSeconds;

    public MicrophoneComponent() : base()
    {
        WaveformType = "PWL";
        SignalBandwidth = 20000.0;
        Amplitude = 1.0;
        Offset = 0.0;
        RebuildAudioWaveform();
    }

    public MicrophoneComponent(string name, string? audioFilePath = null, double bandwidth = 20000.0, double amplitude = 1.0) : this()
    {
        Name = name;
        Reference = name;
        SignalBandwidth = bandwidth;
        Amplitude = amplitude;
        if (!string.IsNullOrWhiteSpace(audioFilePath))
        {
            AudioFilePath = audioFilePath;
        }
        else
        {
            RebuildAudioWaveform();
        }
    }

    /// <summary>
    /// Loads an MP3 or WAV audio file and updates the signal waveform according to the configured bandwidth.
    /// </summary>
    public void LoadAudioFile(string filePath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                _audioData = AudioDecoderService.DecodeAudioFile(filePath, SignalBandwidth);
            }
            else
            {
                _audioData = AudioDecoderService.GenerateAcousticSignal(1.0, 44100, SignalBandwidth);
            }
        }
        catch
        {
            _audioData = AudioDecoderService.GenerateAcousticSignal(1.0, 44100, SignalBandwidth);
        }

        RebuildAudioWaveform();
    }

    /// <summary>
    /// Re-filters the loaded audio signal to match the newly specified bandwidth.
    /// </summary>
    public void SetBandwidth(double bandwidthHz)
    {
        double bw = Math.Clamp(bandwidthHz, 20.0, 100000.0);
        if (_audioData != null && _audioData.Samples.Length > 0)
        {
            double[] filtered = AudioDecoderService.ApplyLowPassFilter(_audioData.Samples, _audioData.SampleRate, bw);
            _audioData = new AudioDecodedData
            {
                Samples = filtered,
                SampleRate = _audioData.SampleRate,
                Channels = _audioData.Channels
            };
        }
        else
        {
            _audioData = AudioDecoderService.GenerateAcousticSignal(1.0, 44100, bw);
        }

        RebuildAudioWaveform();
    }

    /// <summary>
    /// Rebuilds the SPICE Piecewise Linear (PWL) points string from the audio samples.
    /// </summary>
    public void RebuildAudioWaveform()
    {
        WaveformType = "PWL";
        var data = AudioData;
        PwlPoints = AudioDecoderService.BuildPwlString(data.Samples, data.SampleRate, Amplitude, Offset);
        base.Voltage = Offset;
    }

    public override ComponentPin? GetPin(string pinNameOrId)
    {
        var pin = base.GetPin(pinNameOrId);
        if (pin != null) return pin;

        if (pinNameOrId.Equals("out", StringComparison.OrdinalIgnoreCase) ||
            pinNameOrId.Equals("sig", StringComparison.OrdinalIgnoreCase) ||
            pinNameOrId.Equals("audio", StringComparison.OrdinalIgnoreCase) ||
            pinNameOrId.Equals("mic+", StringComparison.OrdinalIgnoreCase))
        {
            return Pins.Find(p => p.Id.Equals("positive", StringComparison.OrdinalIgnoreCase));
        }

        if (pinNameOrId.Equals("gnd", StringComparison.OrdinalIgnoreCase) ||
            pinNameOrId.Equals("ground", StringComparison.OrdinalIgnoreCase) ||
            pinNameOrId.Equals("com", StringComparison.OrdinalIgnoreCase) ||
            pinNameOrId.Equals("mic-", StringComparison.OrdinalIgnoreCase))
        {
            return Pins.Find(p => p.Id.Equals("negative", StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
