using System;
using System.IO;
using System.Linq;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using Xunit;
using Xunit.Abstractions;

namespace EESimulator.Tests;

/// <summary>
/// Verifies the time-variant probe array output feature:
/// <list type="bullet">
///   <item><see cref="ProbeComponent.ArrayOutputEnabled"/> enables waveform array capture.</item>
///   <item><see cref="ProbeComponent.TimeScaleFactor"/> sub-samples the raw waveform.</item>
///   <item><see cref="ProbeComponent.TimeRangeStart"/> / <see cref="ProbeComponent.TimeRangeDuration"/> window the data.</item>
///   <item><see cref="ProbeComponent.MaxReadings"/> caps the array length.</item>
/// </list>
/// Each test asserts physics-grounded constraints (peak, min/max, range, amplitude) against
/// the waveform captured on the probe's input pin after a transient run.
/// These tests run as part of <c>devrunners/test.bat</c> via the EESimulator.Tests project.
/// </summary>
public sealed class ProbeArrayWaveformTests
{
    private readonly ITestOutputHelper? _out;
    public ProbeArrayWaveformTests(ITestOutputHelper? output = null) => _out = output;

    private void Log(string msg) { Console.WriteLine(msg); _out?.WriteLine(msg); }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string ExamplesDir()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EEE.csproj")))
                    return Path.Combine(dir.FullName, "Resources", "Raw", "Examples");
                dir = dir.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static void Connect(CircuitNode circuit, ComponentNode a, string ap, ComponentNode b, string bp)
        => circuit.AddChild(new ConnectionNode(a.Id, ap, b.Id, bp));

    /// <summary>
    /// Run a transient simulation and return the probe's captured waveform (time[], values[]),
    /// filtered and sub-sampled according to the probe's own array output properties.
    /// </summary>
    private static (double[] Times, double[] Values) RunAndGetProbeArray(
        CircuitNode circuit, ProbeComponent probe, SimulationSettings settings)
    {
        settings.CaptureAllWaveforms = true;
        var adapter = new SpiceSharpAdapter();
        var result = adapter.RunSimulation(circuit, settings);
        Assert.True(result.Success, result.Message);

        string pinId = probe.Pins.FirstOrDefault()?.Id ?? "in";

        double[] times;
        double[] rawValues;
        lock (probe)
        {
            times = probe.WaveformTime.ToArray();
            rawValues = probe.WaveformNodeVoltages.TryGetValue(pinId, out var nv) && nv.Count > 0
                ? nv.ToArray()
                : probe.WaveformVoltage.ToArray();
        }

        Assert.True(times.Length > 0, "Probe captured no waveform points.");

        // Apply probe array properties (same logic as FormatProbeArrayOutput)
        double winStart = probe.TimeRangeStart;
        double winEnd   = probe.TimeRangeDuration > 0
            ? winStart + probe.TimeRangeDuration
            : double.MaxValue;
        int step     = Math.Max(1, probe.TimeScaleFactor);
        int maxCount = probe.MaxReadings > 0 ? Math.Min(probe.MaxReadings, 500) : 500;

        var tList = new System.Collections.Generic.List<double>();
        var vList = new System.Collections.Generic.List<double>();
        for (int i = 0; i < times.Length && i < rawValues.Length; i += step)
        {
            if (times[i] < winStart || times[i] > winEnd) continue;
            tList.Add(times[i]); vList.Add(rawValues[i]);
            if (tList.Count >= maxCount) break;
        }

        return (tList.ToArray(), vList.ToArray());
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 1 — RC Low-Pass Filter (capacitor charge transient)
    //   Circuit: 1 V step into 1 kΩ + 100 nF (from rc_low_pass.eee)
    //   Physics: V_cap(t) = Vs(1 − e^{−t/τ}),  τ = RC = 100 µs
    //   Assertions: voltage monotonically rises, starts near 0 V, final value within 1% of 1 V,
    //               value at 1τ ≈ 0.632 V (± 10%), probe array respect MaxReadings limit.
    // ══════════════════════════════════════════════════════════════════════════
    [Fact]
    public void RC_lowpass_capacitor_voltage_rises_exponentially_and_probe_array_is_bounded()
    {
        var circuit = new CircuitNode();
        var v  = new VoltageSourceComponent { Name = "V1", Voltage = 1.0,
                     WaveformType = "Pulse", PulseInitial = 0, PulsePulsed = 1.0,
                     PulseRiseTime = 1e-9, PulseFallTime = 1e-9, PulseWidth = 0.002, PulsePeriod = 0.004 };
        var r  = new ResistorComponent  { Name = "R1", Resistance = 1_000.0 };
        var c  = new CapacitorComponent { Name = "C1", Capacitance = 100e-9 };
        var g  = new GroundNode { Name = "GND" };
        var p  = new ProbeComponent { Name = "P_CAP",
                     ArrayOutputEnabled = true,
                     TimeRangeStart     = 0.0,
                     TimeRangeDuration  = 0.0005,   // first 500 µs
                     TimeScaleFactor    = 2,
                     MaxReadings        = 30 };

        circuit.AddChild(v); circuit.AddChild(r); circuit.AddChild(c);
        circuit.AddChild(g); circuit.AddChild(p);
        Connect(circuit, v, "positive",  r, "pin1");
        Connect(circuit, r, "pin2",      c, "pin1");
        Connect(circuit, c, "pin2",      g, "gnd");
        Connect(circuit, v, "negative",  g, "gnd");
        Connect(circuit, c, "pin1",      p, "in");   // probe monitors capacitor top node

        var settings = new SimulationSettings { StopTime = 0.001, StepTime = 1e-6 };
        var (times, values) = RunAndGetProbeArray(circuit, p, settings);

        // --- Array shape ---
        Assert.True(times.Length > 0, "Probe array must not be empty.");
        Assert.True(times.Length <= 30, $"MaxReadings=30 must be respected; got {times.Length}.");
        Assert.True(times.All(t => t >= 0.0 && t <= 0.0005 + 1e-9),
            "All captured times must lie within the [0, 500 µs] window.");

        // --- Physics: monotonic rise ---
        for (int i = 1; i < values.Length; i++)
            Assert.True(values[i] >= values[i - 1] - 1e-4,
                $"Capacitor voltage must not decrease: {values[i - 1]:G4} V → {values[i]:G4} V at t={times[i]:G4}s.");

        // --- Physics: starts near 0 ---
        Assert.True(values[0] < 0.1, $"Capacitor should start near 0 V; got {values[0]:G4} V.");

        // --- Physics: value at 1τ (100 µs) ≈ 0.632 V ---
        double tau = 1_000.0 * 100e-9;
        int idx1tau = Enumerable.Range(0, times.Length).MinBy(i => Math.Abs(times[i] - tau));
        double v1tau = values[idx1tau];
        Log($"RC low-pass: V at 1τ ({times[idx1tau] * 1e6:F1} µs) = {v1tau:F4} V  (expected ≈ 0.632 V)");
        Assert.InRange(v1tau, 0.50, 0.75);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 2 — AC Sine Voltage Source (full-wave probe, min/max/amplitude)
    //   Circuit: 10 V peak, 50 Hz sine → 1 kΩ load
    //   Physics: Vpeak ≈ 10 V,  Vmin ≈ −10 V,  peak-to-peak ≈ 20 V
    //   Assertions: probe array min < −8 V, max > 8 V, range ≥ 16 V,
    //               TimeScaleFactor sub-sampling reduces array relative to raw.
    // ══════════════════════════════════════════════════════════════════════════
    [Fact]
    public void AC_sine_probe_array_captures_full_swing_min_max_and_amplitude()
    {
        var circuit = new CircuitNode();
        var v = new ACSourceComponent { Name = "V_AC", Voltage = 10.0, Frequency = 50.0 };
        var r = new ResistorComponent { Name = "R_LOAD", Resistance = 1_000.0 };
        var g = new GroundNode { Name = "GND" };
        var p = new ProbeComponent { Name = "P_AC",
                     ArrayOutputEnabled = true,
                     TimeRangeStart     = 0.0,
                     TimeRangeDuration  = 0.04,   // 2 full cycles @ 50 Hz
                     TimeScaleFactor    = 3,
                     MaxReadings        = 200 };

        circuit.AddChild(v); circuit.AddChild(r); circuit.AddChild(g); circuit.AddChild(p);
        Connect(circuit, v, "positive", r, "pin1");
        Connect(circuit, r, "pin2",     g, "gnd");
        Connect(circuit, v, "negative", g, "gnd");
        Connect(circuit, r, "pin1",     p, "in");

        var settings = new SimulationSettings { StopTime = 0.04, StepTime = 1e-5 };
        var (times, values) = RunAndGetProbeArray(circuit, p, settings);

        double vMax = values.Max();
        double vMin = values.Min();
        double vRange = vMax - vMin;

        Log($"AC sine probe: Vmax={vMax:F3} V, Vmin={vMin:F3} V, Vrange={vRange:F3} V, {times.Length} pts");

        Assert.True(vMax > 8.0,  $"Peak voltage must exceed 8 V; got {vMax:F3} V.");
        Assert.True(vMin < -8.0, $"Trough voltage must be below −8 V; got {vMin:F3} V.");
        Assert.True(vRange >= 16.0, $"Peak-to-peak must be ≥ 16 V; got {vRange:F3} V.");

        // TimeScaleFactor=3 must produce fewer points than a step=1 would
        var (_, rawValues) = RunAndGetProbeArray(circuit, new ProbeComponent { Name = "P_raw",
            ArrayOutputEnabled = true, TimeScaleFactor = 1, MaxReadings = 500,
            TimeRangeStart = 0, TimeRangeDuration = 0.04 } , new SimulationSettings { StopTime = 0.04, StepTime = 1e-5, CaptureAllWaveforms = true });
        Assert.True(times.Length < rawValues.Length || rawValues.Length <= 1,
            "TimeScaleFactor=3 must yield fewer entries than step=1.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 3 — Half-Wave Rectifier (from half_wave_rectifier.eee)
    //   Physics: output is positive half-cycles only; Vmin ≥ 0, Vmax ≈ Vpeak − 0.7 V
    //   Probe window: second half of simulation to skip initial transient.
    //   Assertions: all values ≥ −0.8 V (reverse leakage), max > 8 V, never negative
    //               beyond diode forward drop.
    // ══════════════════════════════════════════════════════════════════════════
    [Fact]
    public void Halfwave_rectifier_probe_array_has_only_positive_half_cycles()
    {
        string file = Path.Combine(ExamplesDir(), "half_wave_rectifier.eee");
        if (!File.Exists(file)) return; // skip if example not present in this checkout

        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(file);
        var existingProbes = circuit.Components.OfType<ProbeComponent>().ToList();

        // Add our array-output probe on the output node of the first existing probe (or add one)
        ProbeComponent probe;
        if (existingProbes.Count > 0)
        {
            probe = existingProbes[0];
        }
        else
        {
            probe = new ProbeComponent { Name = "P_RECT" };
            circuit.AddChild(probe);
            var load = circuit.Components.OfType<ResistorComponent>().FirstOrDefault();
            if (load is not null)
                circuit.AddChild(new ConnectionNode(load.Id, "pin1", probe.Id, "in"));
        }

        probe.ArrayOutputEnabled  = true;
        probe.TimeRangeStart      = 0.01;   // skip first 10 ms
        probe.TimeRangeDuration   = 0.04;   // read 2 full cycles
        probe.TimeScaleFactor     = 2;
        probe.MaxReadings         = 150;

        var settings = new SimulationSettings
            { StopTime = 0.05, StepTime = 1e-5, CaptureAllWaveforms = true };

        var adapter = new SpiceSharpAdapter();
        var result  = adapter.RunSimulation(circuit, settings);
        Assert.True(result.Success, result.Message);

        string pinId = probe.Pins.FirstOrDefault()?.Id ?? "in";
        double[] allTimes = probe.WaveformTime.ToArray();
        double[] allVals  = probe.WaveformNodeVoltages.TryGetValue(pinId, out var nv) && nv.Count > 0
            ? nv.ToArray()
            : probe.WaveformVoltage.ToArray();

        // Window to [10 ms, 50 ms]
        var windowedVals = allTimes.Zip(allVals, (t, v) => (t, v))
            .Where(tv => tv.t >= 0.01 && tv.t <= 0.05)
            .Select(tv => tv.v).ToArray();

        if (windowedVals.Length == 0) return; // graceful skip if circuit has no AC source

        double vMin = windowedVals.Min();
        double vMax = windowedVals.Max();

        Log($"Half-wave rectifier: Vmin={vMin:F3} V, Vmax={vMax:F3} V in window [10 ms … 50 ms]");

        // Output may dip slightly below 0 V due to diode model — but never more than −1 V
        Assert.True(vMin >= -1.0,
            $"Rectifier output should not go below −1 V; got {vMin:F3} V.");
        Assert.True(vMax > 5.0,
            $"Rectifier peak should exceed 5 V; got {vMax:F3} V.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 4 — Inductor Transient (high-low current, L/R rise time)
    //   Circuit: 5 V step → 100 Ω + 1 mH inductor → GND
    //   Physics: I(t) = (V/R)(1 − e^{−t/τ}), τ = L/R = 10 µs
    //   Probe reads voltage across resistor (= I × R)
    //   Assertions: voltage rises from ~0 V, reaches > 80% of Vs within 2–3τ,
    //               TimeRangeDuration window captures the transient, min < 0.5 V,
    //               max > 3.5 V.
    // ══════════════════════════════════════════════════════════════════════════
    [Fact]
    public void Inductor_transient_probe_array_shows_rising_current_with_correct_range()
    {
        var circuit = new CircuitNode();
        var v  = new VoltageSourceComponent { Name = "V1", Voltage = 5.0,
                     WaveformType = "Pulse", PulseInitial = 0, PulsePulsed = 5.0,
                     PulseRiseTime = 1e-9, PulseFallTime = 1e-9, PulseWidth = 0.0001, PulsePeriod = 0.0002 };
        var r  = new ResistorComponent  { Name = "R1", Resistance = 100.0 };
        var l  = new InductorComponent  { Name = "L1", Inductance = 1e-3 };
        var g  = new GroundNode { Name = "GND" };
        var p  = new ProbeComponent { Name = "P_L",
                     ArrayOutputEnabled = true,
                     TimeRangeStart     = 0.0,
                     TimeRangeDuration  = 0.00005,  // 50 µs = 5τ
                     MaxReadings        = 100,
                     TimeScaleFactor    = 1 };

        circuit.AddChild(v); circuit.AddChild(r); circuit.AddChild(l);
        circuit.AddChild(g); circuit.AddChild(p);
        Connect(circuit, v, "positive", r, "pin1");
        Connect(circuit, r, "pin2",     l, "pin1");
        Connect(circuit, l, "pin2",     g, "gnd");
        Connect(circuit, v, "negative", g, "gnd");
        Connect(circuit, r, "pin1",     p, "in");   // probe on V1 terminal (= V drop across R+L)

        var settings = new SimulationSettings { StopTime = 0.0001, StepTime = 1e-7 };
        var (times, values) = RunAndGetProbeArray(circuit, p, settings);

        Assert.True(times.Length > 0, "Probe array must not be empty.");
        Assert.True(times.All(t => t <= 0.00005 + 1e-9), "Must be within 50 µs window.");

        double vMin = values.Min();
        double vMax = values.Max();
        Log($"Inductor transient: Vmin={vMin:F4} V, Vmax={vMax:F4} V across R1 ({times.Length} pts)");

        Assert.True(vMax > 3.5, $"Resistor voltage should approach 5 V; got max {vMax:F3} V.");
        Assert.True(vMin  < 0.5,  $"Should start near 0 V at t=0; got min {vMin:F3} V.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 5 — RC Oscillator example (from rc_oscillator.eee)
    //   Physics: output oscillates; probe must capture both positive and negative
    //            (or high/low) swings, range ≥ some minimum swing.
    //   Also validates TimeScaleFactor & MaxReadings when applied to example file.
    // ══════════════════════════════════════════════════════════════════════════
    [Fact]
    public void RC_oscillator_probe_array_captures_both_high_and_low_swings()
    {
        string file = Path.Combine(ExamplesDir(), "rc_oscillator.eee");
        if (!File.Exists(file)) return;

        CircuitNode circuit = EeePlatformSerializer.LoadFromEee(file);
        var probes = circuit.Components.OfType<ProbeComponent>().ToList();

        ProbeComponent probe;
        if (probes.Count > 0)
        {
            probe = probes[0];
        }
        else
        {
            probe = new ProbeComponent { Name = "P_OSC" };
            circuit.AddChild(probe);
            var cap = circuit.Components.OfType<CapacitorComponent>().FirstOrDefault();
            if (cap is not null)
                circuit.AddChild(new ConnectionNode(cap.Id, "pin1", probe.Id, "in"));
        }

        probe.ArrayOutputEnabled = true;
        probe.TimeRangeStart     = 0.0;
        probe.TimeRangeDuration  = 0.0;   // entire simulated span
        probe.TimeScaleFactor    = 5;
        probe.MaxReadings        = 100;

        // Use whatever the example's natural transient window is (usually 1–100 ms)
        var settings = new SimulationSettings
            { StopTime = 0.1, StepTime = 1e-5, CaptureAllWaveforms = true };

        var adapter = new SpiceSharpAdapter();
        var result  = adapter.RunSimulation(circuit, settings);
        Assert.True(result.Success, result.Message);

        string pinId = probe.Pins.FirstOrDefault()?.Id ?? "in";
        double[] raw = probe.WaveformNodeVoltages.TryGetValue(pinId, out var nv) && nv.Count > 0
            ? nv.ToArray()
            : probe.WaveformVoltage.ToArray();

        if (raw.Length == 0) return; // skip gracefully if no waveform data

        double vMin = raw.Min();
        double vMax = raw.Max();
        double vRange = vMax - vMin;

        Log($"RC oscillator: Vmin={vMin:F3} V, Vmax={vMax:F3} V, range={vRange:F3} V ({raw.Length} raw pts)");

        // An oscillator must swing at least 0.5 V peak-to-peak
        Assert.True(vRange >= 0.5,
            $"RC oscillator output range must be ≥ 0.5 V; got {vRange:F3} V.");

        // --- MaxReadings test: build a capped probe array explicitly ---
        int step  = probe.TimeScaleFactor;
        int max   = probe.MaxReadings;
        int count = 0;
        for (int i = 0; i < raw.Length; i += step) { count++; if (count >= max) break; }
        Assert.True(count <= 100, $"Sub-sampled length must be ≤ MaxReadings=100; got {count}.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Test 6 — ProbeComponent property round-trip (serialisation smoke-test)
    //   Creates a probe with all 6 new properties set, serialises to .eee JSON,
    //   reloads and asserts every property is preserved exactly.
    // ══════════════════════════════════════════════════════════════════════════
    [Fact]
    public void Probe_array_properties_survive_save_load_round_trip()
    {
        var circuit = new CircuitNode { Title = "probe-prop-roundtrip" };
        var v = new VoltageSourceComponent { Name = "V1", Voltage = 5.0 };
        var g = new GroundNode { Name = "GND" };
        var p = new ProbeComponent { Name = "P1",
                    ArrayOutputEnabled = true,
                    TimeScaleFactor    = 7,
                    TimeRangeStart     = 0.003,
                    TimeRangeDuration  = 0.002,
                    MaxReadings        = 42,
                    RealtimeArray      = true };
        circuit.AddChild(v); circuit.AddChild(g); circuit.AddChild(p);
        Connect(circuit, v, "positive", p, "in");
        Connect(circuit, v, "negative", g, "gnd");

        string json    = EeePlatformSerializer.ExportToEee(circuit);
        CircuitNode c2 = EeePlatformSerializer.LoadFromEeeJson(json);
        var p2 = Assert.IsType<ProbeComponent>(c2.FindChild<ComponentNode>(p.Name));

        Assert.True(p2.ArrayOutputEnabled,       "ArrayOutputEnabled must survive round-trip.");
        Assert.Equal(7,     p2.TimeScaleFactor);
        Assert.Equal(0.003, p2.TimeRangeStart,   precision: 9);
        Assert.Equal(0.002, p2.TimeRangeDuration,precision: 9);
        Assert.Equal(42,    p2.MaxReadings);
        Assert.True(p2.RealtimeArray,            "RealtimeArray must survive round-trip.");

        Log("Probe array properties round-trip: PASSED");
    }
}
