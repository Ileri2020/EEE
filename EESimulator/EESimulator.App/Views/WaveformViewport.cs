using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Graphics;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>
/// Oscilloscope / Waveform canvas rendering time-domain voltage and current curves
/// for the selected component probed via SpiceSharp simulation.
/// </summary>
public class WaveformViewport : GraphicsView, IDrawable
{
    public static readonly BindableProperty MonitoredComponentProperty =
        BindableProperty.Create(nameof(MonitoredComponent), typeof(ComponentNode), typeof(WaveformViewport),
            null, propertyChanged: (b, _, __) => ((WaveformViewport)b).Invalidate());

    public ComponentNode? MonitoredComponent
    {
        get => (ComponentNode?)GetValue(MonitoredComponentProperty);
        set => SetValue(MonitoredComponentProperty, value);
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

    public WaveformViewport()
    {
        Drawable = this;
        BackgroundColor = Color.FromArgb("#0A0E14");
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
                lock (signal.Component)
                {
                    var values = signal.Kind switch
                    {
                        WaveformSignalKind.ComponentVoltage => signal.Component.WaveformVoltage,
                        WaveformSignalKind.ComponentCurrent => signal.Component.WaveformCurrent,
                        WaveformSignalKind.PinVoltage when signal.Component.WaveformNodeVoltages.TryGetValue(signal.SignalId, out var pinValues) => pinValues,
                        _ => new System.Collections.Generic.List<double>()
                    };
                    return (Signal: signal, Times: signal.Component.WaveformTime.ToList(), Values: values.ToList());
                }
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
        var voltageValues = traces.Where(trace => trace.Signal.Kind != WaveformSignalKind.ComponentCurrent)
            .SelectMany(trace => trace.Values).ToList();
        var currentValues = traces.Where(trace => trace.Signal.Kind == WaveformSignalKind.ComponentCurrent)
            .SelectMany(trace => trace.Values).ToList();
        static (double Min, double Span) GetScale(System.Collections.Generic.List<double> values)
        {
            if (values.Count == 0) return (0, 1);
            double min = values.Min();
            double span = values.Max() - min;
            if (span < 1e-15)
            {
                min -= 0.5;
                span = 1.0;
            }
            return (min, span);
        }
        var voltageScale = GetScale(voltageValues);
        var currentScale = GetScale(currentValues);
        float topPad = 34f;
        float botPad = 20f;
        float plotH = h - topPad - botPad;
        float laneHeight = hasVoltage && hasCurrent ? plotH / 2f : plotH;
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
                float x1 = (float)index / (count - 1) * w;
                float y1 = laneTop + currentLaneHeight - (float)((trace.Values[index] - scale.Min) / scale.Span) * currentLaneHeight;
                float x2 = (float)(index + 1) / (count - 1) * w;
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
        canvas.DrawString($"{comp.Reference} · {traces.Count} trace(s) · {traceTimeRange(traces):G4} s",
            8, h - 17, w - 16, 14, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    private static double traceTimeRange(System.Collections.Generic.List<(WaveformSignal Signal, System.Collections.Generic.List<double> Times, System.Collections.Generic.List<double> Values)> traces) =>
        traces.SelectMany(trace => trace.Times).DefaultIfEmpty(0).Max();
}
