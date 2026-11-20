using EEE.Services;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
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
        ["crystal_oscillator.eee", "TP_OUT", 0.005, 0.005, 5, 100, 0.01, 1e-6],
        ["wien_bridge_oscillator.eee", "TP_BRIDGE", 0.01, 0.01, 2, 80, 0.02, 1e-4],
        ["wien_bridge_function_generator.eee", "TP_OUT", 0.01, 0.01, 2, 80, 0.02, 1e-4],
        ["voltage_controlled_oscillator.eee", "TP_TRIANGLE", 0.0, 0.005, 2, 100, 0.005, 2.5e-5],
        ["triangle_wave_generator.eee", "TP_OUT", 0.04, 0.01, 13, 80, 0.05, 1e-5],
        ["sawtooth_wave_generator.eee", "TP_OUT", 0.0, 0.005, 1, 80, 0.005, 2.5e-5],
        ["555_timer_monostable.eee", "TP_OUT", 0.004, 0.061, 100, 60, 0.065, 2.5e-5],
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
        string signalDiagnostics = filename is "voltage_controlled_oscillator.eee" or "crystal_oscillator.eee"
            ? string.Join("; ", circuit.Components
                .OfType<OpAmpComponent>()
                .Select(opAmp => $"{opAmp.Name}: " + string.Join(", ", opAmp.WaveformNodeVoltages
                    .Where(node => node.Value.Count > 0)
                    .Select(node => $"{node.Key}={node.Value.Min():G5}..{node.Value.Max():G5}"))))
            : string.Empty;

        Assert.NotEmpty(values);
        Assert.Equal(times.Length, values.Length);
        Assert.True(times.Length <= maxReadings, $"{filename}/{probeName} exceeded MaxReadings.");
        Assert.All(times, time =>
            Assert.InRange(time, rangeStart, rangeStart + rangeDuration + 1e-9));
        Assert.All(values, value => Assert.True(double.IsFinite(value), $"{filename}/{probeName} captured {value}."));

        // Verify probe discoverability properties: min, max, amplitude range, frequency, and oscillation state
        Assert.True(double.IsFinite(probe.Minimum), $"{filename}/{probeName}: Minimum must be finite.");
        Assert.True(double.IsFinite(probe.Maximum), $"{filename}/{probeName}: Maximum must be finite.");
        Assert.True(probe.Maximum >= probe.Minimum, $"{filename}/{probeName}: Maximum must be >= Minimum.");
        Assert.Equal(probe.AmplitudeRange, probe.Maximum - probe.Minimum, precision: 3);
        Assert.Equal(probe.PeakToPeak, probe.AmplitudeRange, precision: 3);

        // Verify the probe discovers that the signal oscillates and is not flat DC
        Assert.True(probe.AmplitudeRange >= 0.01,
            $"{filename}/{probeName} should exhibit signal variation (amplitude range: {probe.AmplitudeRange:G4}), not flat DC. {signalDiagnostics}");
        Assert.True(probe.IsOscillating || probe.AmplitudeRange >= 0.05,
            $"{filename}/{probeName} failed oscillation discovery: IsOscillating={probe.IsOscillating}, Vpp={probe.AmplitudeRange:G4}, Freq={probe.Frequency:F1}Hz. {signalDiagnostics}");
    }

    [Fact]
    public void DC_reference_probe_discovers_zero_frequency_and_no_oscillation()
    {
        string exampleFile = Path.Combine(FindExamplesDirectory(), "rc_low_pass.eee");
        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(exampleFile);
        var refProbe = Assert.IsType<ProbeComponent>(
            circuit.Components.Single(c => c.Name == "TP_REFERENCE"));

        var settings = new SimulationSettings
        {
            Analysis = AnalysisType.Transient,
            StopTime = 0.001,
            StepTime = 1e-5,
            CaptureAllWaveforms = true
        };

        var (_, values) = ProbeArrayWaveformTests.RunAndGetProbeArray(circuit, refProbe, settings);
        Assert.NotEmpty(values);
        Assert.False(refProbe.IsOscillating, "DC ground reference probe must discover that it is NOT oscillating.");
        Assert.InRange(refProbe.AmplitudeRange, 0.0, 1e-4);
        Assert.Equal(0.0, refProbe.Frequency);
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
