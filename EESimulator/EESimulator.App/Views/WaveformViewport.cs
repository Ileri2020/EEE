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

        // 2. Grid lines (10 horizontal, 8 vertical divisions)
        canvas.StrokeColor = Color.FromArgb("#14261C");
        canvas.StrokeSize = 1f;

        const int numX = 10;
        const int numY = 8;
        float dx = w / numX;
        float dy = h / numY;

        for (int i = 1; i < numX; i++)
            canvas.DrawLine(i * dx, 0, i * dx, h);

        for (int j = 1; j < numY; j++)
            canvas.DrawLine(0, j * dy, w, j * dy);

        // Center crosshair (dotted/dashed feel)
        canvas.StrokeColor = Color.FromArgb("#1F3B2C");
        canvas.StrokeSize = 1.2f;
        canvas.DrawLine(w / 2f, 0, w / 2f, h);
        canvas.DrawLine(0, h / 2f, w, h / 2f);

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

        static (double Min, double Span) ComputeScale(System.Collections.Generic.IEnumerable<(WaveformSignal Signal, System.Collections.Generic.IReadOnlyList<double> Times, System.Collections.Generic.IReadOnlyList<double> Values)> traceList, bool isCurrent)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            bool found = false;

            foreach (var trace in traceList)
            {
                bool match = (trace.Signal.Kind == WaveformSignalKind.ComponentCurrent) == isCurrent;
                if (!match) continue;

                var vals = trace.Values;
                for (int i = 0; i < vals.Count; i++)
                {
                    double v = vals[i];
                    if (v < min) min = v;
                    if (v > max) max = v;
                    found = true;
                }
            }

            if (!found) return (0, 1);
            double span = max - min;
            if (span < 1e-15)
            {
                min -= 0.5;
                span = 1.0;
            }
            return (min, span);
        }

        var voltageScale = ComputeScale(traces, false);
        var currentScale = ComputeScale(traces, true);
        float topPad = 34f;
        float botPad = 20f;
        float plotH = h - topPad - botPad;
        float laneHeight = hasVoltage && hasCurrent ? plotH / 2f : plotH;

        double globalMinTime = double.MaxValue;
        double globalMaxTime = double.MinValue;
        foreach (var trace in traces)
        {
            if (trace.Times.Count > 0)
            {
                if (trace.Times[0] < globalMinTime) globalMinTime = trace.Times[0];
                if (trace.Times[^1] > globalMaxTime) globalMaxTime = trace.Times[^1];
            }
        }
        double timeSpan = Math.Max(globalMaxTime - globalMinTime, 1e-6);

        int colorIndex = 0;
        foreach (var trace in traces)
        {
            int count = Math.Min(trace.Times.Count, trace.Values.Count);
            bool current = trace.Signal.Kind == WaveformSignalKind.ComponentCurrent;
            var scale = current ? currentScale : voltageScale;
            float laneTop = hasVoltage && hasCurrent && current ? topPad + laneHeight : topPad;
            float currentLaneHeight = hasVoltage && hasCurrent ? laneHeight : plotH;
            canvas.StrokeColor = TraceColors[colorIndex % TraceColors.Length];
            canvas.StrokeSize = 1.8f;
            for (int index = 0; index < count - 1; index++)
            {
                float x1 = (float)((trace.Times[index] - globalMinTime) / timeSpan) * w;
                float y1 = laneTop + currentLaneHeight - (float)((trace.Values[index] - scale.Min) / scale.Span) * currentLaneHeight;
                float x2 = (float)((trace.Times[index + 1] - globalMinTime) / timeSpan) * w;
                float y2 = laneTop + currentLaneHeight - (float)((trace.Values[index + 1] - scale.Min) / scale.Span) * currentLaneHeight;
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

        if (hasVoltage && hasCurrent)
        {
            canvas.StrokeColor = Color.FromArgb("#30363D");
            canvas.StrokeSize = 1;
            canvas.DrawLine(0, topPad + laneHeight, w, topPad + laneHeight);
        }
        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = 10;
        canvas.DrawString($"{comp.Reference} · {traces.Count} trace(s) · {globalMinTime:G4} s → {globalMaxTime:G4} s (Δ {timeSpan:G4} s)",
            8, h - 17, w - 16, 14, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    private static double traceTimeRange(System.Collections.Generic.List<(WaveformSignal Signal, System.Collections.Generic.IReadOnlyList<double> Times, System.Collections.Generic.IReadOnlyList<double> Values)> traces) =>
        traces.SelectMany(trace => trace.Times).DefaultIfEmpty(0).Max();
}
