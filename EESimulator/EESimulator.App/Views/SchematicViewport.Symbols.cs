using Microsoft.Maui.Graphics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

public partial class SchematicViewport
{
    private static void DrawResistor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#F78166");
        canvas.StrokeSize = 2f;
        // Lead lines
        canvas.DrawLine(-20, 0, -10, 0);
        canvas.DrawLine(10, 0, 20, 0);
        // Zigzag
        var pts = new[] { -10f, -8f, -6f, 8f, -2f, -8f, 2f, 8f, 6f, -8f, 10f, 0f };
        for (int i = 0; i < pts.Length - 2; i += 2)
            canvas.DrawLine(pts[i], pts[i + 1], pts[i + 2], pts[i + 3]);
    }

    private static void DrawCapacitor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-15, 0, -4, 0);
        canvas.DrawLine(4, 0, 15, 0);
        canvas.DrawLine(-4, -12, -4, 12);
        canvas.DrawLine(4, -12, 4, 12);
    }

    private static void DrawInductor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -14, 0);
        canvas.DrawLine(14, 0, 20, 0);
        // Coil arcs
        for (int i = -12; i <= 6; i += 6)
        {
            canvas.DrawArc(i, -6, 6, 6, 180, 360, false, false);
        }
    }

    private static void DrawVoltageSource(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(0, 0, 14);
        canvas.DrawLine(0, -20, 0, -14);
        canvas.DrawLine(0, 14, 0, 20);
        // + / - symbols
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 11;
        canvas.DrawString("+", -4, -12, 8, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("−", -4, 0, 8, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawGround(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(0, -10, 0, 0);
        canvas.DrawLine(-14, 0, 14, 0);
        canvas.DrawLine(-9, 5, 9, 5);
        canvas.DrawLine(-5, 10, 5, 10);
    }

    private static void DrawCurrentSource(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(0, 0, 14);
        canvas.DrawLine(0, -20, 0, -14);
        canvas.DrawLine(0, 14, 0, 20);
        // Arrow pointing downward
        canvas.DrawLine(0, -7, 0, 7);
        canvas.DrawLine(-4, 2, 0, 7);
        canvas.DrawLine(4, 2, 0, 7);
    }

    private static void DrawDiode(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#FF7B72");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -8, 0);
        canvas.DrawLine(8, 0, 20, 0);
        // Triangle (anode to cathode)
        var path = new PathF();
        path.MoveTo(-8, -10);
        path.LineTo(8, 0);
        path.LineTo(-8, 10);
        path.Close();
        canvas.DrawPath(path);
        // Cathode bar
        canvas.DrawLine(8, -10, 8, 10);
    }

    private static void DrawLED(ICanvas canvas)
    {
        DrawDiode(canvas);
        // Radiation arrows
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(3, -12, 10, -18);
        canvas.DrawLine(7, -18, 10, -18);
        canvas.DrawLine(10, -15, 10, -18);

        canvas.DrawLine(8, -8, 15, -14);
        canvas.DrawLine(12, -14, 15, -14);
        canvas.DrawLine(15, -11, 15, -14);
    }

    private static void DrawBJT(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#FFA657");
        canvas.StrokeSize = 2f;
        // Base bar
        canvas.DrawLine(-20, 0, -6, 0);
        canvas.DrawLine(-6, -14, -6, 14);
        // Collector lead
        canvas.DrawLine(-6, -7, 15, -20);
        // Emitter lead with arrow
        canvas.DrawLine(-6, 7, 15, 20);
        canvas.DrawLine(7, 16, 15, 20);
        canvas.DrawLine(11, 10, 15, 20);
    }

    private static void DrawMOSFET(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 2f;
        // Gate lead & bar
        canvas.DrawLine(-20, 0, -8, 0);
        canvas.DrawLine(-8, -14, -8, 14);
        // Channel plate segments
        canvas.DrawLine(-3, -14, -3, -6);
        canvas.DrawLine(-3, -4, -3, 4);
        canvas.DrawLine(-3, 6, -3, 14);
        // Drain and Source
        canvas.DrawLine(-3, -10, 15, -20);
        canvas.DrawLine(-3, 10, 15, 20);
    }

    private static void DrawOpAmp(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        // Triangle body
        var path = new PathF();
        path.MoveTo(-25, -20);
        path.LineTo(20, 0);
        path.LineTo(-25, 20);
        path.Close();
        canvas.DrawPath(path);
        // Output lead
        canvas.DrawLine(20, 0, 25, 0);
        // + / - signs
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.FontSize = 10;
        canvas.DrawString("−", -22, -16, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("+", -22, 6, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawAndGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        // Inputs
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        // AND Body
        canvas.DrawLine(-12, -16, -12, 16);
        canvas.DrawLine(-12, -16, 0, -16);
        canvas.DrawLine(-12, 16, 0, 16);
        canvas.DrawArc(-16, -16, 32, 32, 270, 90, false, false);
        // Output
        canvas.DrawLine(16, 0, 20, 0);
    }

    private static void DrawOrGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        // Inputs
        canvas.DrawLine(-20, -10, -10, -10);
        canvas.DrawLine(-20, 10, -10, 10);
        // Curved back
        canvas.DrawArc(-26, -16, 26, 32, 270, 90, false, false);
        // Top and bottom pointed curves
        canvas.DrawLine(-13, -16, 2, -12);
        canvas.DrawLine(2, -12, 16, 0);
        canvas.DrawLine(-13, 16, 2, 12);
        canvas.DrawLine(2, 12, 16, 0);
        // Output
        canvas.DrawLine(16, 0, 20, 0);
    }

    private static void DrawNotGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        // Input
        canvas.DrawLine(-20, 0, -10, 0);
        // Triangle
        var path = new PathF();
        path.MoveTo(-10, -14);
        path.LineTo(10, 0);
        path.LineTo(-10, 14);
        path.Close();
        canvas.DrawPath(path);
        // Bubble
        canvas.DrawCircle(13, 0, 3);
        // Output
        canvas.DrawLine(16, 0, 20, 0);
    }

    private static void DrawNandGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(-12, -16, -12, 16);
        canvas.DrawLine(-12, -16, -2, -16);
        canvas.DrawLine(-12, 16, -2, 16);
        canvas.DrawArc(-16, -16, 28, 32, 270, 90, false, false);
        // Inversion bubble
        canvas.DrawCircle(14, 0, 3);
        canvas.DrawLine(17, 0, 20, 0);
    }

    private static void DrawNorGate(ICanvas canvas)
    {
        DrawOrGate(canvas);
        // Inversion bubble
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.DrawCircle(18, 0, 3);
        canvas.DrawLine(21, 0, 25, 0);
    }

    private static void DrawXorGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        // Extra input curve
        canvas.DrawArc(-30, -16, 26, 32, 270, 90, false, false);
        // Inputs
        canvas.DrawLine(-20, -10, -14, -10);
        canvas.DrawLine(-20, 10, -14, 10);
        // Inner curved back
        canvas.DrawArc(-24, -16, 26, 32, 270, 90, false, false);
        canvas.DrawLine(-11, -16, 2, -12);
        canvas.DrawLine(2, -12, 16, 0);
        canvas.DrawLine(-11, 16, 2, 12);
        canvas.DrawLine(2, 12, 16, 0);
        // Output
        canvas.DrawLine(16, 0, 20, 0);
    }

    private static void DrawXnorGate(ICanvas canvas)
    {
        DrawXorGate(canvas);
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.DrawCircle(18, 0, 3);
        canvas.DrawLine(21, 0, 25, 0);
    }

    private static void DrawIC(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(-20, -22, 40, 44, 4);
        // Notch at top
        canvas.DrawArc(-4, -22, 8, 8, 0, 180, false, false);
        // Pins
        for (int y = -15; y <= 15; y += 10)
        {
            canvas.DrawLine(-25, y, -20, y);
            canvas.DrawLine(20, y, 25, y);
        }
    }

    private void DrawJunction(ICanvas canvas, ComponentNode comp)
    {
        var pin = comp.Pins.FirstOrDefault();
        double voltage = pin != null ? Math.Abs(SamplePinVoltage(comp, pin)) : 0.0;
        double current = pin != null
            ? Math.Abs(SamplePinCurrent(comp, pin, comp.LiveCurrent))
            : Math.Abs(comp.LiveCurrent);
        Color c = GetElectricalStateColor(voltage, current);
        canvas.FillColor = c;
        canvas.FillCircle(0, 0, 4.5f);
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 1.0f;
        canvas.DrawCircle(0, 0, 4.5f);
    }

    private static void DrawLabel(ICanvas canvas, string netName)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1.5f;
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-18, -10, 36, 20, 4);
        canvas.DrawRoundedRectangle(-18, -10, 36, 20, 4);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 9;
        canvas.DrawString(string.IsNullOrEmpty(netName) ? "NET" : netName,
            -18, -9, 36, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static void DrawGenericComponent(ICanvas canvas, string type)
    {
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(-16, -12, 32, 24, 4);
        canvas.FontColor = Color.FromArgb("#E3B341");
        canvas.FontSize = 8;
        canvas.DrawString(type[..Math.Min(3, type.Length)].ToUpper(),
            -14, -6, 28, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private void DrawViewportLabel(ICanvas canvas, RectF dirtyRect)
    {
        // Draw in screen space (no world transform active here)
        canvas.FontColor = Color.FromArgb("#30405A");
        canvas.FontSize = 10;
        float lx = dirtyRect.Right - 90;
        float ly = dirtyRect.Bottom - 20;
        canvas.DrawString($"Zoom: {_zoom:P0}", lx, ly, 88, 16,
            HorizontalAlignment.Right, VerticalAlignment.Center);
    }

}
