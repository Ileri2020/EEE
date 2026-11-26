using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Graphics;
using EESimulator.Core.Models;
using EESimulator.Engine.Simulation;

namespace EESimulator.App.Views;

/// <summary>
/// Oscilloscope / Waveform canvas rendering time-domain voltage and current curves
/// for the selected component probed via SpiceSharp simulation.
/// </summary>
public class WaveformViewport : GraphicsView, IDrawable
{
    public static readonly BindableProperty MonitoredComponentProperty =
        BindableProperty.Create(nameof(MonitoredComponent), typeof(ComponentNode), typeof(WaveformViewport),
            null, propertyChanged: (b, _, _) =>
            {
                var vp = (WaveformViewport)b;
                vp._rollingBuffers.Clear();
                vp._lastDrawnEnvelopes.Clear();
                vp.Invalidate();
            });

    public ComponentNode? MonitoredComponent
    {
        get => (ComponentNode?)GetValue(MonitoredComponentProperty);
        set => SetValue(MonitoredComponentProperty, value);
    }

    public static readonly BindableProperty SimulationSnapshotProperty =
        BindableProperty.Create(nameof(SimulationSnapshot), typeof(SimulationSnapshot), typeof(WaveformViewport), null,
            propertyChanged: (b, _, n) => ((WaveformViewport)b).OnSimulationSnapshotChanged(n as SimulationSnapshot));

    public SimulationSnapshot? SimulationSnapshot
    {
        get => (SimulationSnapshot?)GetValue(SimulationSnapshotProperty);
        set => SetValue(SimulationSnapshotProperty, value);
    }

    public static readonly BindableProperty SignalsProperty =
        BindableProperty.Create(nameof(Signals), typeof(ObservableCollection<WaveformSignal>), typeof(WaveformViewport), null,
            propertyChanged: (b, oldValue, newValue) => ((WaveformViewport)b).OnSignalsChanged(
                oldValue as ObservableCollection<WaveformSignal>, newValue as ObservableCollection<WaveformSignal>));

    public ObservableCollection<WaveformSignal>? Signals
    {
        get => (ObservableCollection<WaveformSignal>?)GetValue(SignalsProperty);
        set => SetValue(SignalsProperty, value);
    }

    public static readonly BindableProperty TimePerGridProperty =
        BindableProperty.Create(nameof(TimePerGrid), typeof(double), typeof(WaveformViewport), 0.1d,
            validateValue: (_, value) => value is double number && double.IsFinite(number) && number > 0,
            propertyChanged: (b, _, _) => ((WaveformViewport)b).Invalidate());

    public double TimePerGrid
    {
        get => (double)GetValue(TimePerGridProperty);
        set => SetValue(TimePerGridProperty, value);
    }

    public static readonly BindableProperty MagnitudePerGridProperty =
        BindableProperty.Create(nameof(MagnitudePerGrid), typeof(double), typeof(WaveformViewport), 1.0d,
            validateValue: (_, value) => value is double number && double.IsFinite(number) && number > 0,
            propertyChanged: (b, _, _) => ((WaveformViewport)b).Invalidate());

    public double MagnitudePerGrid
    {
        get => (double)GetValue(MagnitudePerGridProperty);
        set => SetValue(MagnitudePerGridProperty, value);
    }

    private static readonly Color[] TraceColors =
    {
        Color.FromArgb("#56D364"), Color.FromArgb("#79C0FF"), Color.FromArgb("#FF7B72"),
        Color.FromArgb("#D2A8FF"), Color.FromArgb("#E3B341"), Color.FromArgb("#F778BA"),
        Color.FromArgb("#39D353"), Color.FromArgb("#FFA657")
    };

    // Rolling circular buffers of historical (Time, Value) points per signal key.
    // Retains full array history so the graph continues rendering smoothly even when
    // solver call rates reduce or when values stabilize.
    private readonly Dictionary<string, (List<double> Times, List<double> Values)> _rollingBuffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (double Min, double Max, int Count)> _lastDrawnEnvelopes = new(StringComparer.OrdinalIgnoreCase);

    public WaveformViewport()
    {
        Drawable = this;
        BackgroundColor = Color.FromArgb("#0A0E14");
    }

    private static string GetSignalKey(WaveformSignal signal) =>
        $"{signal.Component.Id}:{signal.SignalId}:{signal.Kind}";

    private void OnSimulationSnapshotChanged(SimulationSnapshot? snapshot)
    {
        if (snapshot == null) return;
        bool bufferUpdated = UpdateRollingBuffers(snapshot);
        if (bufferUpdated)
        {
            Invalidate();
        }
    }

    private bool UpdateRollingBuffers(SimulationSnapshot snapshot)
    {
        if (Signals == null || Signals.Count == 0) return false;
        int maxPoints = Math.Max(SimulationPerformanceProfile.Current.MaxWaveformPoints, 128);
        bool anyBufferUpdated = false;

        foreach (var signal in Signals.Where(s => s.IsSelected))
        {
            string key = GetSignalKey(signal);
            if (!_rollingBuffers.TryGetValue(key, out var buffer))
            {
                buffer = (new List<double>(maxPoints), new List<double>(maxPoints));
                _rollingBuffers[key] = buffer;
            }

            // Case 1: Transient waveform data array available from solver
            if (snapshot.ComponentWaveforms.TryGetValue(signal.Component.Id, out var waveform))
            {
                var values = signal.Kind switch
                {
                    WaveformSignalKind.ComponentVoltage => waveform.Voltages,
                    WaveformSignalKind.ComponentCurrent => waveform.Currents,
                    WaveformSignalKind.PinVoltage when waveform.NodeVoltages.TryGetValue(signal.SignalId, out var pinValues) => pinValues,
                    _ => Array.Empty<double>()
                };

                if (waveform.Times.Count >= 2 && values.Count >= 2)
                {
                    if (buffer.Times.Count == 0 || waveform.Times[0] <= buffer.Times[^1] - 0.001)
                    {
                        // New transient run or sweep reset
                        buffer.Times.Clear();
                        buffer.Values.Clear();
                        int start = Math.Max(0, waveform.Times.Count - maxPoints);
                        for (int i = start; i < waveform.Times.Count; i++)
                        {
                            buffer.Times.Add(waveform.Times[i]);
                            buffer.Values.Add(values[i]);
                        }
                        anyBufferUpdated = true;
                    }
                    else
                    {
                        double lastT = buffer.Times[^1];
                        bool added = false;
                        for (int i = 0; i < waveform.Times.Count; i++)
                        {
                            if (waveform.Times[i] > lastT + 1e-9)
                            {
                                buffer.Times.Add(waveform.Times[i]);
                                buffer.Values.Add(values[i]);
                                added = true;
                            }
                        }
                        while (buffer.Times.Count > maxPoints)
                        {
                            buffer.Times.RemoveAt(0);
                            buffer.Values.RemoveAt(0);
                        }
                        if (added)
                            anyBufferUpdated = true;
                    }
                    continue;
                }
            }

            // Case 2: Only DC / Live operating point state available
            if (snapshot.ComponentStates.TryGetValue(signal.Component.Id, out var state))
            {
                double val = signal.Kind switch
                {
                    WaveformSignalKind.ComponentVoltage => state.LiveVoltage,
                    WaveformSignalKind.ComponentCurrent => state.LiveCurrent,
                    WaveformSignalKind.PinVoltage when state.PinVoltages.TryGetValue(signal.SignalId, out var pv) => pv,
                    _ => 0.0
                };
                double t = snapshot.DeviceTimeSeconds;

                if (buffer.Times.Count == 0)
                {
                    buffer.Times.Add(Math.Max(0, t - 0.02));
                    buffer.Values.Add(val);
                    buffer.Times.Add(t);
                    buffer.Values.Add(val);
                    anyBufferUpdated = true;
                }
                else
                {
                    if (t > buffer.Times[^1] + 1e-6)
                    {
                        buffer.Times.Add(t);
                        buffer.Values.Add(val);
                        while (buffer.Times.Count > maxPoints)
                        {
                            buffer.Times.RemoveAt(0);
                            buffer.Values.RemoveAt(0);
                        }
                        anyBufferUpdated = true;
                    }
                    else if (Math.Abs(buffer.Values[^1] - val) > 1e-6)
                    {
                        buffer.Values[^1] = val;
                        anyBufferUpdated = true;
                    }
                }
            }
        }

        return anyBufferUpdated;
    }

    private bool CheckEnvelopeChange(string key, List<double> values)
    {
        if (values.Count == 0) return true;
        double min = double.MaxValue;
        double max = double.MinValue;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] < min) min = values[i];
            if (values[i] > max) max = values[i];
        }

        double span = Math.Max(max - min, 1e-6);
        double fraction = SimulationPerformanceProfile.Current.GraphStabilityFraction;

        if (_lastDrawnEnvelopes.TryGetValue(key, out var prev))
        {
            if (Math.Abs(min - prev.Min) < span * fraction &&
                Math.Abs(max - prev.Max) < span * fraction &&
                Math.Abs(values.Count - prev.Count) <= 1)
            {
                return false; // Stable oscillation; skip redundant graph invalidation
            }
        }

        _lastDrawnEnvelopes[key] = (min, max, values.Count);
        return true;
    }

    private void OnSignalsChanged(ObservableCollection<WaveformSignal>? oldSignals, ObservableCollection<WaveformSignal>? newSignals)
    {
        if (oldSignals != null)
        {
            oldSignals.CollectionChanged -= OnSignalCollectionChanged;
            foreach (var signal in oldSignals) signal.PropertyChanged -= OnSignalPropertyChanged;
        }
        if (newSignals != null)
        {
            newSignals.CollectionChanged += OnSignalCollectionChanged;
            foreach (var signal in newSignals) signal.PropertyChanged += OnSignalPropertyChanged;
        }
        _rollingBuffers.Clear();
        _lastDrawnEnvelopes.Clear();
        Invalidate();
    }

    private void OnSignalCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (WaveformSignal signal in e.OldItems) signal.PropertyChanged -= OnSignalPropertyChanged;
        if (e.NewItems != null)
            foreach (WaveformSignal signal in e.NewItems) signal.PropertyChanged += OnSignalPropertyChanged;
        Invalidate();
    }

    private void OnSignalPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Invalidate();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float w = dirtyRect.Width;
        float h = dirtyRect.Height;

        if (w <= 0 || h <= 0) return;

        // 1. Oscilloscope dark background
        canvas.FillColor = Color.FromArgb("#0A0E14");
        canvas.FillRectangle(0, 0, w, h);

        const int numX = 10;
        const int numY = 8;
        var comp = MonitoredComponent;
        var traces = (Signals ?? new ObservableCollection<WaveformSignal>())
            .Where(signal => signal.IsSelected)
            .Select(signal =>
            {
                string key = GetSignalKey(signal);
                if (_rollingBuffers.TryGetValue(key, out var buffer) && buffer.Times.Count >= 2 && buffer.Values.Count >= 2)
                {
                    return (Signal: signal, Times: (IReadOnlyList<double>)buffer.Times, Values: (IReadOnlyList<double>)buffer.Values);
                }

                if (SimulationSnapshot?.ComponentWaveforms.TryGetValue(signal.Component.Id, out var waveform) == true)
                {
                    var values = signal.Kind switch
                    {
                        WaveformSignalKind.ComponentVoltage => waveform.Voltages,
                        WaveformSignalKind.ComponentCurrent => waveform.Currents,
                        WaveformSignalKind.PinVoltage when waveform.NodeVoltages.TryGetValue(signal.SignalId, out var pinValues) => pinValues,
                        _ => Array.Empty<double>()
                    };
                    return (Signal: signal, Times: waveform.Times, Values: values);
                }

                return (Signal: signal, Times: (IReadOnlyList<double>)Array.Empty<double>(), Values: (IReadOnlyList<double>)Array.Empty<double>());
            })
            .Where(trace => trace.Times.Count >= 2 && trace.Values.Count >= 2)
            .ToList();

        if (comp == null || traces.Count == 0)
        {
            canvas.FontColor = Color.FromArgb("#484F58");
            canvas.FontSize = 12;
            canvas.DrawString(comp == null
                ? "No component selected for waveform view. Toggle 📈 on a component."
                : "No transient samples yet. Run the simulation to populate the selected traces.",
                0, 0, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        bool hasVoltage = traces.Any(trace => trace.Signal.Kind != WaveformSignalKind.ComponentCurrent);
        bool hasCurrent = traces.Any(trace => trace.Signal.Kind == WaveformSignalKind.ComponentCurrent);
        const float leftPad = 48f;
        const float rightPad = 8f;
        const float topPad = 24f;
        const float bottomPad = 22f;
        float plotLeft = leftPad;
        float plotRight = Math.Max(plotLeft + 1, w - rightPad);
        float plotTop = topPad;
        float plotBottom = Math.Max(plotTop + 1, h - bottomPad);
        float plotWidth = plotRight - plotLeft;
        float plotHeight = plotBottom - plotTop;
        float laneHeight = hasVoltage && hasCurrent ? plotHeight / 2f : plotHeight;
        float divisionWidth = plotWidth / numX;
        float divisionHeight = laneHeight / numY;
        double timePerDivision = TimePerGrid;
        double magnitudePerDivision = MagnitudePerGrid;
        double visibleDuration = timePerDivision * numX;

        canvas.StrokeColor = Color.FromArgb("#14261C");
        canvas.StrokeSize = 1f;
        for (int i = 0; i <= numX; i++)
        {
            float x = plotLeft + i * divisionWidth;
            canvas.DrawLine(x, plotTop, x, plotBottom);
        }

        for (int lane = 0; lane < (hasVoltage && hasCurrent ? 2 : 1); lane++)
        {
            float laneTop = plotTop + lane * laneHeight;
            float zeroY = laneTop + laneHeight / 2f;
            for (int division = 0; division <= numY; division++)
            {
                float y = laneTop + division * divisionHeight;
                canvas.DrawLine(plotLeft, y, plotRight, y);
            }

            canvas.StrokeColor = Color.FromArgb("#1F3B2C");
            canvas.StrokeSize = 1.2f;
            canvas.DrawLine(plotLeft, zeroY, plotRight, zeroY);

            canvas.FontColor = Color.FromArgb("#8B949E");
            canvas.FontSize = 8;
            for (int division = 0; division <= numY; division += 2)
            {
                double value = (numY / 2.0 - division) * magnitudePerDivision;
                float y = laneTop + division * divisionHeight - 6;
                canvas.DrawString($"{value:G4}", 1, y, leftPad - 5, 12,
                    HorizontalAlignment.Right, VerticalAlignment.Center);
            }
        }

        canvas.StrokeColor = Color.FromArgb("#385B45");
        canvas.StrokeSize = 1.2f;
        canvas.DrawLine(plotLeft, plotTop, plotLeft, plotBottom);

        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = 8;
        for (int division = 0; division <= numX; division += 2)
        {
            float x = plotLeft + division * divisionWidth;
            canvas.DrawString($"{division * timePerDivision:G4}s", x - 24, h - bottomPad + 4, 48, 12,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = 8;
        canvas.DrawString($"X {timePerDivision:G4}s/div · Y {magnitudePerDivision:G4} { (hasVoltage && hasCurrent ? "units" : hasCurrent ? "A" : "V")}/div",
            plotLeft + 4, 2, Math.Max(0, w - plotLeft - 8), 16,
            HorizontalAlignment.Left, VerticalAlignment.Center);

        double globalMinTime = traces.SelectMany(trace => trace.Times).DefaultIfEmpty(0).Min();
        double globalMaxTime = traces.SelectMany(trace => trace.Times).DefaultIfEmpty(0).Max();

        canvas.SaveState();
        canvas.ClipRectangle(plotLeft, plotTop, plotWidth, plotHeight);
        int colorIndex = 0;
        foreach (var trace in traces)
        {
            int count = Math.Min(trace.Times.Count, trace.Values.Count);
            bool current = trace.Signal.Kind == WaveformSignalKind.ComponentCurrent;
            float laneTop = hasVoltage && hasCurrent && current ? plotTop + laneHeight : plotTop;
            float zeroY = laneTop + laneHeight / 2f;
            canvas.StrokeColor = TraceColors[colorIndex % TraceColors.Length];
            canvas.StrokeSize = 1.8f;
            for (int index = 0; index < count - 1; index++)
            {
                float x1 = plotLeft + (float)(trace.Times[index] / visibleDuration) * plotWidth;
                float y1 = zeroY - (float)(trace.Values[index] / magnitudePerDivision) * divisionHeight;
                float x2 = plotLeft + (float)(trace.Times[index + 1] / visibleDuration) * plotWidth;
                float y2 = zeroY - (float)(trace.Values[index + 1] / magnitudePerDivision) * divisionHeight;
                canvas.DrawLine(x1, y1, x2, y2);
            }

            if (colorIndex < 5)
            {
                canvas.FontColor = TraceColors[colorIndex % TraceColors.Length];
                canvas.FontSize = 8;
                canvas.DrawString(trace.Signal.Label, 8 + colorIndex * Math.Max(105, w / 4), 4,
                    Math.Max(100, w / 4 - 8), 18, HorizontalAlignment.Left, VerticalAlignment.Center);
            }
            colorIndex++;
        }
        canvas.RestoreState();

        if (hasVoltage && hasCurrent)
        {
            canvas.StrokeColor = Color.FromArgb("#30363D");
            canvas.StrokeSize = 1;
            canvas.DrawLine(plotLeft, plotTop + laneHeight, plotRight, plotTop + laneHeight);
        }
        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = 10;
        canvas.DrawString($"{comp.Reference} · {traces.Count} trace(s) · {globalMinTime:G4} s → {globalMaxTime:G4} s · visible {visibleDuration:G4} s",
            8, h - 17, w - 16, 14, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    private static double traceTimeRange(System.Collections.Generic.List<(WaveformSignal Signal, System.Collections.Generic.IReadOnlyList<double> Times, System.Collections.Generic.IReadOnlyList<double> Values)> traces) =>
        traces.SelectMany(trace => trace.Times).DefaultIfEmpty(0).Max();
}
