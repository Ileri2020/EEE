using EEE.Services;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using Xunit;

namespace EESimulator.Tests;

public sealed class OscillatingExampleProbeTests
{
    public static IEnumerable<object[]> OscillatingExamples =>
    [
        ["colpitts_oscillator.eee", "TP_DIVIDER", 0.0001, 0.0001, 2, 100, 0.0002, 1e-6],
        ["hartley_oscillator.eee", "TP_TANK", 0.0002, 0.0001, 2, 80, 0.0003, 1e-6],
        ["crystal_oscillator.eee", "TP_OUT", 0.001, 0.0005, 5, 100, 0.0015, 5e-6],
        ["wien_bridge_oscillator.eee", "TP_BRIDGE", 0.01, 0.01, 2, 80, 0.02, 1e-4],
        ["wien_bridge_function_generator.eee", "TP_OUT", 0.01, 0.01, 2, 80, 0.02, 1e-4],
        ["voltage_controlled_oscillator.eee", "TP_TRIANGLE", 0.0, 0.005, 2, 100, 0.005, 2.5e-5],
        ["triangle_wave_generator.eee", "TP_OUT", 0.0, 0.004, 2, 80, 0.004, 2e-5],
        ["sawtooth_wave_generator.eee", "TP_OUT", 0.0, 0.005, 1, 80, 0.005, 2.5e-5],
        ["555_timer_monostable.eee", "TP_OUT", 0.0, 0.005, 1, 60, 0.005, 2.5e-5],
        ["schmitt_trigger.eee", "TP_OUT", 0.0, 0.02, 2, 60, 0.02, 1e-4],
    ];

    [Theory]
    [MemberData(nameof(OscillatingExamples))]
    public void Example_array_probe_captures_its_configured_time_window(
        string filename,
        string probeName,
        double rangeStart,
        double rangeDuration,
        int timeScaleFactor,
        int maxReadings,
        double stopTime,
        double stepTime)
    {
        string exampleFile = Path.Combine(FindExamplesDirectory(), filename);
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var probe = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(component => component.Name == probeName));

        probe.ArrayOutputEnabled = true;
        probe.TimeRangeStart = rangeStart;
        probe.TimeRangeDuration = rangeDuration;
        probe.TimeScaleFactor = timeScaleFactor;
        probe.MaxReadings = maxReadings;

        var settings = new SimulationSettings
        {
            Analysis = AnalysisType.Transient,
            StopTime = stopTime,
            StepTime = stepTime,
            CaptureAllWaveforms = true
        };
        var (times, values) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, probe, settings);

        Assert.NotEmpty(values);
        Assert.Equal(times.Length, values.Length);
        Assert.True(times.Length <= maxReadings, $"{filename}/{probeName} exceeded MaxReadings.");
        Assert.All(times, time =>
            Assert.InRange(time, rangeStart, rangeStart + rangeDuration + 1e-9));
        Assert.All(values, value => Assert.True(double.IsFinite(value), $"{filename}/{probeName} captured {value}."));
    }

    private static string FindExamplesDirectory()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EEE.csproj")))
                return Path.Combine(directory.FullName, "Resources", "Raw", "Examples");
        }

        throw new DirectoryNotFoundException("Could not locate repository examples.");
    }
}
