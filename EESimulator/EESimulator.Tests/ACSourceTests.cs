using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Registry;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using Xunit;

namespace EESimulator.Tests;

public sealed class ACSourceTests
{
    [Fact]
    public void Registry_creates_a_single_phase_source_with_230_volt_rms_at_50_hz()
    {
        var source = Assert.IsType<ACSourceComponent>(
            ComponentRegistry.CreateInstance(ACSourceComponent.StableType, "VAC1"));

        Assert.Equal(230, source.Voltage, 6);
        Assert.Equal(50, source.Frequency, 6);
        Assert.Equal("Sine", source.WaveformType);
        Assert.Equal(0, source.Offset, 6);
    }

    [Fact]
    public void Pulse_voltage_and_offset_remain_independent_and_survive_eee_round_trip()
    {
        var circuit = new CircuitNode();
        var source = new ACSourceComponent
        {
            Name = "VAC1",
            WaveformType = "Pulse",
            Frequency = 60,
            Offset = 12,
            Voltage = 24,
            PhaseDegrees = 30
        };
        circuit.AddChild(source);

        CircuitNode restoredCircuit = EeePlatformSerializer.LoadFromEeeJson(
            EeePlatformSerializer.ExportToEee(circuit));
        var restored = Assert.IsType<ACSourceComponent>(restoredCircuit.FindChild<ComponentNode>("VAC1"));

        Assert.Equal(24, restored.Voltage, 6);
        Assert.Equal(60, restored.Frequency, 6);
        Assert.Equal(12, restored.Offset, 6);
        Assert.Equal(30, restored.PhaseDegrees, 6);
        Assert.Equal("Pulse", restored.WaveformType);
    }

    [Fact]
    public void Sine_source_simulates_its_configured_rms_voltage()
    {
        var circuit = new CircuitNode();
        var source = new ACSourceComponent
        {
            Name = "VAC1",
            Voltage = 120,
            Frequency = 60,
            WaveformType = "Sine"
        };
        var resistor = new ResistorComponent { Name = "R1", Resistance = 1000 };
        var ground = new GroundNode { Name = "GND" };
        circuit.AddChild(source);
        circuit.AddChild(resistor);
        circuit.AddChild(ground);
        circuit.AddChild(new ConnectionNode(source.Id, "positive", resistor.Id, "pin1"));
        circuit.AddChild(new ConnectionNode(resistor.Id, "pin2", ground.Id, "gnd"));
        circuit.AddChild(new ConnectionNode(source.Id, "negative", ground.Id, "gnd"));

        var settings = new SimulationSettings
        {
            StopTime = 1.0 / 60,
            StepTime = 1.0 / 20000,
            CaptureAllWaveforms = true
        };
        var result = new SpiceSharpAdapter().RunSimulation(circuit, settings);

        Assert.True(result.Success, result.Message);
        Assert.NotEmpty(source.WaveformTime);
        Assert.Equal(source.WaveformTime.Count, source.WaveformVoltage.Count);
        int quarterCycle = Enumerable.Range(0, source.WaveformTime.Count)
            .MinBy(index => Math.Abs(source.WaveformTime[index] - 1.0 / 240));
        Assert.InRange(
            source.WaveformVoltage[quarterCycle],
            120 * Math.Sqrt(2) - 1,
            120 * Math.Sqrt(2) + 1);
    }
}
