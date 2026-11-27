using System;
using System.IO;
using EESimulator.Commands;
using EESimulator.Core.Components.Audio;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Registry;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using EESimulator.Scripting.API;
using Xunit;

namespace EESimulator.Tests;

public sealed class MicrophoneComponentTests
{
    [Fact]
    public void Registry_creates_microphone_with_defaults_and_aliases()
    {
        var mic1 = Assert.IsType<MicrophoneComponent>(
            ComponentRegistry.CreateInstance(MicrophoneComponent.StableType, "MIC1"));

        Assert.Equal(20000.0, mic1.SignalBandwidth);
        Assert.Equal(1.0, mic1.Amplitude);
        Assert.Equal(0.0, mic1.Offset);
        Assert.Equal("PWL", mic1.WaveformType);
        Assert.False(string.IsNullOrWhiteSpace(mic1.PwlPoints));

        // Test aliases
        var mic2 = Assert.IsType<MicrophoneComponent>(ComponentRegistry.CreateInstance("microphone", "MIC2"));
        var mic3 = Assert.IsType<MicrophoneComponent>(ComponentRegistry.CreateInstance("mic", "MIC3"));
        var mic4 = Assert.IsType<MicrophoneComponent>(ComponentRegistry.CreateInstance("audio.microphone", "MIC4"));

        Assert.NotNull(mic2);
        Assert.NotNull(mic3);
        Assert.NotNull(mic4);
    }

    [Fact]
    public void Microphone_pin_aliases_resolve_correctly()
    {
        var mic = new MicrophoneComponent("MIC1");

        Assert.NotNull(mic.GetPin("positive"));
        Assert.NotNull(mic.GetPin("negative"));
        Assert.Same(mic.GetPin("positive"), mic.GetPin("out"));
        Assert.Same(mic.GetPin("positive"), mic.GetPin("sig"));
        Assert.Same(mic.GetPin("positive"), mic.GetPin("mic+"));
        Assert.Same(mic.GetPin("negative"), mic.GetPin("gnd"));
        Assert.Same(mic.GetPin("negative"), mic.GetPin("mic-"));
    }

    [Fact]
    public void Bandwidth_filter_attenuates_frequencies_above_cutoff()
    {
        int sampleRate = 44100;
        int length = 4410; // 0.1 s
        double[] input = new double[length];
        double fHigh = 10000.0; // 10 kHz signal

        for (int i = 0; i < length; i++)
        {
            double t = (double)i / sampleRate;
            input[i] = Math.Sin(2.0 * Math.PI * fHigh * t);
        }

        // Apply low-pass filter with 2 kHz bandwidth
        double[] filtered = AudioDecoderService.ApplyLowPassFilter(input, sampleRate, 2000.0);

        // Peak magnitude of 10 kHz signal must be significantly attenuated
        double maxOriginal = 0;
        double maxFiltered = 0;
        for (int i = 500; i < length; i++) // skip filter warmup
        {
            maxOriginal = Math.Max(maxOriginal, Math.Abs(input[i]));
            maxFiltered = Math.Max(maxFiltered, Math.Abs(filtered[i]));
        }

        Assert.True(maxOriginal > 0.9);
        Assert.True(maxFiltered < 0.25, $"Expected significant attenuation at 10 kHz, but got peak {maxFiltered}");
    }

    [Fact]
    public void Wav_decoder_successfully_parses_pcm_stream()
    {
        int sampleRate = 22050;
        short[] pcmSamples = new short[2205]; // 0.1s at 22050 Hz
        for (int i = 0; i < pcmSamples.Length; i++)
        {
            pcmSamples[i] = (short)(Math.Sin(2.0 * Math.PI * 440.0 * i / sampleRate) * 16000);
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + pcmSamples.Length * 2);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16); // Subchunk1Size
        writer.Write((short)1); // PCM format
        writer.Write((short)1); // Mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // ByteRate
        writer.Write((short)2); // BlockAlign
        writer.Write((short)16); // BitsPerSample
        writer.Write("data".ToCharArray());
        writer.Write(pcmSamples.Length * 2);
        for (int i = 0; i < pcmSamples.Length; i++)
            writer.Write(pcmSamples[i]);

        byte[] wavBytes = ms.ToArray();
        var decoded = AudioDecoderService.DecodeWav(wavBytes);

        Assert.Equal(sampleRate, decoded.SampleRate);
        Assert.Equal(1, decoded.Channels);
        Assert.Equal(pcmSamples.Length, decoded.Samples.Length);
        Assert.True(Math.Abs(decoded.Samples[10] - pcmSamples[10] / 32768.0) < 0.01);
    }

    [Fact]
    public void Microphone_simulates_in_circuit_with_load_resistor()
    {
        var circuit = new CircuitNode();
        var mic = new MicrophoneComponent("MIC1", bandwidth: 8000.0, amplitude: 2.0);
        var resistor = new ResistorComponent { Name = "R1", Resistance = 1000.0 };
        var ground = new GroundNode { Name = "GND" };

        circuit.AddChild(mic);
        circuit.AddChild(resistor);
        circuit.AddChild(ground);

        // Connect mic positive to resistor pin1, resistor pin2 to ground, mic negative to ground
        circuit.AddChild(new ConnectionNode(mic.Id, "positive", resistor.Id, "pin1"));
        circuit.AddChild(new ConnectionNode(resistor.Id, "pin2", ground.Id, "gnd"));
        circuit.AddChild(new ConnectionNode(mic.Id, "negative", ground.Id, "gnd"));

        var settings = new SimulationSettings
        {
            StopTime = 1e-3,
            StepTime = 1e-5,
            CaptureAllWaveforms = true
        };

        var engine = new SpiceSharpAdapter();
        var results = engine.RunSimulation(circuit, settings);

        Assert.True(results.Success, results.Message);
        Assert.NotEmpty(mic.WaveformTime);
        Assert.Equal(mic.WaveformTime.Count, mic.WaveformVoltage.Count);
        Assert.NotEmpty(resistor.WaveformTime);
    }

    [Fact]
    public void Python_api_creates_and_configures_microphone()
    {
        var circuit = new CircuitNode();
        var cmdManager = new CommandManager();
        var api = new PythonApi(cmdManager, circuit);

        api.AddMicrophone("MIC_UUT", audioFile: "voice.mp3", bandwidth: 4000.0, amplitude: 0.75);

        var comp = circuit.FindChild<MicrophoneComponent>("MIC_UUT");
        Assert.NotNull(comp);
        Assert.Equal(4000.0, comp.SignalBandwidth);
        Assert.Equal(0.75, comp.Amplitude);
        Assert.Equal("voice.mp3", comp.AudioFilePath);
    }
}

