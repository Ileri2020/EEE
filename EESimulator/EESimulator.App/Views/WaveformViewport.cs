using System;
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

    public WaveformViewport()
    {
        Drawable = this;
        BackgroundColor = Color.FromArgb("#0A0E14");
    }

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

        // 3. Draw Waveforms if component is present and has data
        var comp = MonitoredComponent;
        if (comp == null || comp.WaveformTime.Count < 2)
        {
            // Placeholder notice
            canvas.FontColor = Color.FromArgb("#484F58");
            canvas.FontSize = 12;
            canvas.DrawString(comp == null
                ? "No component selected for waveform view. Toggle 📈 on a component."
                : $"Awaiting waveform data for {comp.Reference}… Run simulation.",
                0, 0, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        var tList = comp.WaveformTime;
        var vList = comp.WaveformVoltage;
        var iList = comp.WaveformCurrent;

        int count = Math.Min(tList.Count, Math.Min(vList.Count, iList.Count));

        // Voltage scaling
        double maxV = vList.Take(count).DefaultIfEmpty(5.0).Max();
        double minV = vList.Take(count).DefaultIfEmpty(0.0).Min();
        double spanV = Math.Max(maxV - minV, 0.5);

        // Current scaling
        double maxI = iList.Take(count).DefaultIfEmpty(0.01).Max();
        double minI = iList.Take(count).DefaultIfEmpty(0.0).Min();
        double spanI = Math.Max(maxI - minI, 0.0001);

        // Margin for HUD headers
        float topPad = 26f;
        float botPad = 18f;
        float plotH = h - topPad - botPad;

        // Draw Voltage Curve (CH1: Neon Green #56D364)
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        for (int idx = 0; idx < count - 1; idx++)
        {
            float x1 = (float)idx / (count - 1) * w;
            float y1 = topPad + plotH - (float)((vList[idx] - minV) / spanV) * plotH;
            float x2 = (float)(idx + 1) / (count - 1) * w;
            float y2 = topPad + plotH - (float)((vList[idx + 1] - minV) / spanV) * plotH;
            canvas.DrawLine(x1, y1, x2, y2);
        }

        // Draw Current Curve (CH2: Cyan #79C0FF)
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 1.6f;
        for (int idx = 0; idx < count - 1; idx++)
        {
            float x1 = (float)idx / (count - 1) * w;
            float y1 = topPad + plotH - (float)((iList[idx] - minI) / spanI) * plotH;
            float x2 = (float)(idx + 1) / (count - 1) * w;
            float y2 = topPad + plotH - (float)((iList[idx + 1] - minI) / spanI) * plotH;
            canvas.DrawLine(x1, y1, x2, y2);
        }

        // 4. Oscilloscope HUD readout
        canvas.FillColor = Color.FromArgb("#CC0D1117");
        canvas.FillRoundedRectangle(8, 6, 260, 20, 4);

        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 10;
        canvas.DrawString($"CH1 V: {comp.LiveVoltage:F2}V pk-pk:{spanV:F2}V", 14, 7, 130, 16, HorizontalAlignment.Left, VerticalAlignment.Center);

        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString($"CH2 I: {comp.LiveCurrent * 1000:F1}mA", 146, 7, 115, 16, HorizontalAlignment.Left, VerticalAlignment.Center);

        // Component name tag in upper right
        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = 10;
        canvas.DrawString($"PROBE: {comp.Reference} ({comp.ComponentType})", w - 180, 7, 170, 16, HorizontalAlignment.Right, VerticalAlignment.Center);
    }
}
