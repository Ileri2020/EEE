using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

public static partial class ComponentSymbolDrawer
{
    public static void DrawXorGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawArc(-30, -16, 26, 32, 270, 90, false, false);
        canvas.DrawLine(-20, -10, -14, -10);
        canvas.DrawLine(-20, 10, -14, 10);
        canvas.DrawArc(-24, -16, 26, 32, 270, 90, false, false);
        canvas.DrawLine(-11, -16, 2, -12);
        canvas.DrawLine(2, -12, 16, 0);
        canvas.DrawLine(-11, 16, 2, 12);
        canvas.DrawLine(2, 12, 16, 0);
        canvas.DrawLine(16, 0, 20, 0);
    }

    public static void DrawXnorGate(ICanvas canvas)
    {
        DrawXorGate(canvas);
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.DrawCircle(18, 0, 3);
        canvas.DrawLine(21, 0, 25, 0);
    }

    public static void DrawIC(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(-20, -22, 40, 44, 4);
        canvas.DrawArc(-4, -22, 8, 8, 0, 180, false, false);
        for (int y = -15; y <= 15; y += 10)
        {
            canvas.DrawLine(-25, y, -20, y);
            canvas.DrawLine(20, y, 25, y);
        }
    }

    public static void DrawTtlPackage(ICanvas canvas, string partNumber, bool showInternalStructure)
    {
        int pinCount = EESimulator.Core.Components.Electronics.TtlPackageComponent.GetPackagePinCount(partNumber);
        int pinsPerSide = pinCount / 2;
        const float bodyLeft = -18f;
        const float bodyRight = 18f;
        const float bodyTop = -22f;
        const float bodyHeight = 44f;

        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 1.6f;
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(bodyLeft, bodyTop, bodyRight - bodyLeft, bodyHeight, 3f);
        canvas.DrawRoundedRectangle(bodyLeft, bodyTop, bodyRight - bodyLeft, bodyHeight, 3f);

        // DIP notch marks pin 1 at the upper-left corner.
        canvas.FillColor = Color.FromArgb("#0D1117");
        canvas.FillRectangle(-4f, bodyTop - 1f, 8f, 3f);
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.DrawArc(-4f, bodyTop - 2f, 8f, 8f, 0f, 180f, false, false);

        float pinStep = 36f / Math.Max(1, pinsPerSide - 1);
        canvas.StrokeSize = 1.2f;
        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = pinsPerSide > 8 ? 4.5f : 5.5f;
        for (int index = 0; index < pinsPerSide; index++)
        {
            float y = -18f + index * pinStep;
            int leftPin = index + 1;
            int rightPin = pinCount - index;
            canvas.DrawLine(-30f, y, bodyLeft, y);
            canvas.DrawLine(bodyRight, y, 30f, y);
            canvas.DrawString(leftPin.ToString(), bodyLeft + 1f, y - 3f, 8f, 6f,
                HorizontalAlignment.Left, VerticalAlignment.Center);
            canvas.DrawString(rightPin.ToString(), bodyRight - 9f, y - 3f, 8f, 6f,
                HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.FontSize = showInternalStructure ? 6.5f : 9f;
        canvas.DrawString(partNumber, -15f, showInternalStructure ? -19f : -7f, 30f, showInternalStructure ? 8f : 14f,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        if (!showInternalStructure)
            return;

        var gates = EESimulator.Core.Components.Electronics.TtlPinCatalog.GetGates(partNumber);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 4.5f;
        if (gates.Count == 0)
        {
            canvas.DrawString("74 SERIES", -14f, 4f, 28f, 8f,
                HorizontalAlignment.Center, VerticalAlignment.Center);
            return;
        }

        int visibleGateCount = Math.Min(gates.Count, 4);
        for (int i = 0; i < visibleGateCount; i++)
        {
            string operation = gates[i].Operation;
            canvas.DrawString(operation, -14f, -10f + i * 7f, 28f, 6f,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    public static void DrawWire(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, 20, 0);
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-20, 0, 2.5f);
        canvas.FillCircle(20, 0, 2.5f);
    }

    public static void DrawJunction(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(-16, 0, 16, 0);
        canvas.DrawLine(0, -16, 0, 16);
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(0, 0, 4.5f);
    }

    public static void DrawLabel(ICanvas canvas, string netName)
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

    public static void DrawTextAnnotation(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 1.5f;
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-16, -12, 32, 24, 3);
        canvas.DrawRoundedRectangle(-16, -12, 32, 24, 3);
        canvas.DrawLine(-10, -5, 10, -5);
        canvas.DrawLine(-10, 0, 6, 0);
        canvas.DrawLine(-10, 5, 2, 5);
    }

    public static void DrawConnector(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 1.5f;
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-12, -18, 24, 36, 3);
        canvas.DrawRoundedRectangle(-12, -18, 24, 36, 3);
        for (int y = -10; y <= 10; y += 10)
        {
            canvas.FillColor = Color.FromArgb("#E3B341");
            canvas.FillCircle(0, y, 2.5f);
        }
    }

    public static void DrawGenericComponent(ICanvas canvas, string type)
    {
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(-16, -12, 32, 24, 4);
        canvas.FontColor = Color.FromArgb("#E3B341");
        canvas.FontSize = 8;
        string txt = string.IsNullOrEmpty(type) ? "CMP" : type[..Math.Min(3, type.Length)].ToUpper();
        canvas.DrawString(txt, -14, -6, 28, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawMutualInductance(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 2f;
        // Primary coil loops (left)
        canvas.DrawArc(-20, -14, 10, 10, 90, 270, true, false);
        canvas.DrawArc(-20, -4, 10, 10, 90, 270, true, false);
        canvas.DrawArc(-20, 6, 10, 10, 90, 270, true, false);
        // Secondary coil loops (right)
        canvas.DrawArc(10, -14, 10, 10, 270, 90, true, false);
        canvas.DrawArc(10, -4, 10, 10, 270, 90, true, false);
        canvas.DrawArc(10, 6, 10, 10, 270, 90, true, false);
        // Magnetic core bars (middle)
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 1.2f;
        canvas.DrawLine(-2, -14, -2, 16);
        canvas.DrawLine(2, -14, 2, 16);
    }

    public static void DrawDiamond(ICanvas canvas, Color color)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2f;
        var path = new PathF();
        path.MoveTo(0, -14);
        path.LineTo(14, 0);
        path.LineTo(0, 14);
        path.LineTo(-14, 0);
        path.Close();
        canvas.DrawPath(path);
    }

    public static void DrawVCVS(ICanvas canvas)
    {
        DrawDiamond(canvas, Color.FromArgb("#56D364"));
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(-20, -10, -10, -10);
        canvas.DrawLine(-20, 10, -10, 10);
        canvas.DrawLine(10, -10, 20, -10);
        canvas.DrawLine(10, 10, 20, 10);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 9;
        canvas.DrawString("+", -4, -10, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("−", -4, 0, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawVCCS(ICanvas canvas)
    {
        DrawDiamond(canvas, Color.FromArgb("#56D364"));
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(-20, -10, -10, -10);
        canvas.DrawLine(-20, 10, -10, 10);
        canvas.DrawLine(10, -10, 20, -10);
        canvas.DrawLine(10, 10, 20, 10);
        canvas.DrawLine(0, -7, 0, 7);
        canvas.DrawLine(-3, 2, 0, 7);
        canvas.DrawLine(3, 2, 0, 7);
    }

    public static void DrawCCVS(ICanvas canvas)
    {
        DrawDiamond(canvas, Color.FromArgb("#79C0FF"));
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(-20, -10, -10, -10);
        canvas.DrawLine(-20, 10, -10, 10);
        canvas.DrawLine(10, -10, 20, -10);
        canvas.DrawLine(10, 10, 20, 10);
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.FontSize = 9;
        canvas.DrawString("+", -4, -10, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("−", -4, 0, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawCCCS(ICanvas canvas)
    {
        DrawDiamond(canvas, Color.FromArgb("#79C0FF"));
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(-20, -10, -10, -10);
        canvas.DrawLine(-20, 10, -10, 10);
        canvas.DrawLine(10, -10, 20, -10);
        canvas.DrawLine(10, 10, 20, 10);
        canvas.DrawLine(0, -7, 0, 7);
        canvas.DrawLine(-3, 2, 0, 7);
        canvas.DrawLine(3, 2, 0, 7);
    }

    public static void DrawJFET(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#FFA657");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-4, -14, -4, 14);
        canvas.DrawLine(-20, 0, -4, 0);
        canvas.DrawLine(-11, -4, -4, 0);
        canvas.DrawLine(-11, 4, -4, 0);
        canvas.DrawLine(-4, -10, 15, -20);
        canvas.DrawLine(-4, 10, 15, 20);
    }

    public static void DrawVoltageSwitch(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -8, 0);
        canvas.DrawLine(8, 0, 20, 0);
        canvas.DrawCircle(-7, 0, 2);
        canvas.DrawCircle(7, 0, 2);
        canvas.DrawLine(-6, -2, 6, -11);
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 1.2f;
        canvas.DrawLine(0, -20, 0, -14);
        canvas.DrawLine(0, 14, 0, 20);
    }

    public static void DrawCurrentSwitch(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -8, 0);
        canvas.DrawLine(8, 0, 20, 0);
        canvas.DrawCircle(-7, 0, 2);
        canvas.DrawCircle(7, 0, 2);
        canvas.DrawLine(-6, -2, 6, -11);
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 1.2f;
        canvas.DrawLine(0, -20, 0, -14);
        canvas.DrawLine(0, 14, 0, 20);
    }

    public static void DrawRelay(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -14, -15, -14);
        canvas.DrawLine(-20, 14, -15, 14);
        var coil = new PathF();
        coil.MoveTo(-15, -14);
        coil.LineTo(-9, -10);
        coil.LineTo(-15, -6);
        coil.LineTo(-9, -2);
        coil.LineTo(-15, 2);
        coil.LineTo(-9, 6);
        coil.LineTo(-15, 10);
        coil.LineTo(-15, 14);
        canvas.DrawPath(coil);

        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.DrawLine(20, -14, 8, -14);
        canvas.FillColor = Color.FromArgb("#E3B341");
        canvas.FillCircle(7, -14, 2.2f);
        canvas.DrawLine(7, -14, -2, -3);
        canvas.DrawLine(2, 14, 20, 14);
        canvas.FillCircle(1, 14, 2.2f);
    }

    public static void DrawTransmissionLine(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(12, -10, 20, -10);
        canvas.DrawLine(12, 10, 20, 10);
        canvas.DrawRoundedRectangle(-12, -12, 24, 24, 4);
        canvas.StrokeSize = 1.2f;
        canvas.DrawLine(-10, -5, 10, -5);
        canvas.DrawLine(-10, 5, 10, 5);
        canvas.DrawEllipse(-4, -7, 8, 14);
    }

    public static void DrawVoltageDelay(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(12, -10, 20, -10);
        canvas.DrawLine(12, 10, 20, 10);
        canvas.DrawRoundedRectangle(-12, -14, 24, 28, 4);
        canvas.StrokeSize = 1.2f;
        canvas.DrawCircle(0, 0, 6);
        canvas.DrawLine(0, 0, 0, -4);
        canvas.DrawLine(0, 0, 3, 0);
    }

    public static void DrawClock(ICanvas canvas, bool isOn = false)
    {
        canvas.StrokeColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(-14, -14, 28, 28, 4);

        // Square wave symbol
        canvas.StrokeColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 1.8f;
        var path = new PathF();
        path.MoveTo(-9, 5);
        path.LineTo(-9, -4);
        path.LineTo(-2, -4);
        path.LineTo(-2, 5);
        path.LineTo(5, 5);
        path.LineTo(5, -4);
        path.LineTo(9, -4);
        canvas.DrawPath(path);

        // Output pin lead
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(14, 0, 20, 0);
    }

    public static void DrawPin(ICanvas canvas, bool isOn = false)
    {
        canvas.StrokeColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(-4, 0, 12);

        // Arrow or level
        canvas.StrokeColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 1.8f;
        canvas.DrawLine(-9, 0, 1, 0);
        canvas.DrawLine(-3, -4, 1, 0);
        canvas.DrawLine(-3, 4, 1, 0);

        // Pin lead
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(8, 0, 20, 0);
    }

    public static void DrawPower(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2.2f;
        canvas.DrawLine(0, 15, 0, -4);

        // Upward arrow VCC
        canvas.DrawLine(-10, -4, 0, -14);
        canvas.DrawLine(0, -14, 10, -4);

        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 8f;
        canvas.DrawString("VCC", -15, -24, 30, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawPullResistor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-10, -16, 10, -16);
        canvas.DrawLine(0, -16, 0, -12);

        // Resistor zigzag
        canvas.StrokeColor = Color.FromArgb("#F78166");
        var pts = new[] { 0f, -12f, 4f, -9f, -4f, -5f, 4f, -1f, -4f, 3f, 4f, 7f, 0f, 10f };
        for (int i = 0; i < pts.Length - 2; i += 2)
            canvas.DrawLine(pts[i], pts[i + 1], pts[i + 2], pts[i + 3]);

        canvas.DrawLine(0, 10, 0, 15);

        // Direction arrow
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(8, 2, 8, -6);
        canvas.DrawLine(5, -3, 8, -6);
        canvas.DrawLine(11, -3, 8, -6);
    }

    public static void DrawTunnel(ICanvas canvas, string label = "")
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-15, 0, -7, 0);

        // Pointed tag banner
        var path = new PathF();
        path.MoveTo(-7, -10);
        path.LineTo(9, -10);
        path.LineTo(16, 0);
        path.LineTo(9, 10);
        path.LineTo(-7, 10);
        path.Close();

        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillPath(path);
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1.8f;
        canvas.DrawPath(path);

        string txt = !string.IsNullOrWhiteSpace(label) ? label : "NET";
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 8f;
        canvas.DrawString(txt, -7, -8, 20, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawProbe(ICanvas canvas, bool isOn = false)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-15, 0, -8, 0);

        canvas.StrokeColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#79C0FF");
        canvas.DrawCircle(4, 0, 12);

        canvas.FontColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#79C0FF");
        canvas.FontSize = 10f;
        canvas.DrawString("V", -1, -5, 10, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawDigitalTransistor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#FFA657");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -6, 0);
        canvas.DrawLine(-6, -11, -6, 11);

        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.DrawLine(-1, -13, -1, 13);
        canvas.DrawLine(-1, -10, 8, -10);
        canvas.DrawLine(8, -10, 8, -20);
        canvas.DrawLine(-1, 10, 8, 10);
        canvas.DrawLine(8, 10, 8, 20);
    }

    public static void DrawTransmissionGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -12, 0);
        canvas.DrawLine(12, 0, 20, 0);

        // Upper triangle
        var path1 = new PathF();
        path1.MoveTo(-12, -7);
        path1.LineTo(12, -7);
        path1.LineTo(0, 5);
        path1.Close();

        // Lower triangle
        var path2 = new PathF();
        path2.MoveTo(-12, 7);
        path2.LineTo(12, 7);
        path2.LineTo(0, -5);
        path2.Close();

        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 1.8f;
        canvas.DrawPath(path1);
        canvas.DrawPath(path2);

        // Control leads
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.DrawLine(0, -18, 0, -8);
        canvas.StrokeColor = Color.FromArgb("#F78166");
        canvas.DrawCircle(0, 10, 2);
        canvas.DrawLine(0, 12, 0, 18);
    }

    public static void DrawBitExtender(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -14, 0);
        canvas.DrawLine(14, 0, 20, 0);

        var path = new PathF();
        path.MoveTo(-14, -7);
        path.LineTo(14, -13);
        path.LineTo(14, 13);
        path.LineTo(-14, 7);
        path.Close();

        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillPath(path);
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 1.8f;
        canvas.DrawPath(path);

        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.FontSize = 7.5f;
        canvas.DrawString("EXT", -10, -6, 20, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawConstant(ICanvas canvas, bool isOn = false)
    {
        canvas.StrokeColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2f;
        canvas.DrawRoundedRectangle(-12, -10, 20, 20, 4);

        canvas.FontColor = isOn ? Color.FromArgb("#56D364") : Color.FromArgb("#C9D1D9");
        canvas.FontSize = 10f;
        canvas.DrawString("1", -8, -6, 12, 12, HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.DrawLine(8, 0, 15, 0);
    }

    public static void DrawDoNotConnect(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 1.8f;
        canvas.DrawLine(-10, 0, 0, 0);

        canvas.StrokeColor = Color.FromArgb("#F78166");
        canvas.StrokeSize = 2.5f;
        canvas.DrawLine(-6, -6, 6, 6);
        canvas.DrawLine(6, -6, -6, 6);
    }

    public static void DrawLossyTransmissionLine(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#FFA657");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(12, -10, 20, -10);
        canvas.DrawLine(12, 10, 20, 10);

        canvas.DrawRoundedRectangle(-12, -13, 24, 26, 4);
        canvas.StrokeSize = 1.4f;
        // Resistor-loss top line
        canvas.DrawLine(-9, -6, -5, -8);
        canvas.DrawLine(-5, -8, -1, -4);
        canvas.DrawLine(-1, -4, 3, -8);
        canvas.DrawLine(3, -8, 7, -4);
        canvas.DrawLine(7, -4, 9, -6);
        // Bottom ground reference
        canvas.DrawLine(-9, 6, 9, 6);
    }

    public static void DrawCoaxialCable(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(12, -10, 20, -10);
        canvas.DrawLine(12, 10, 20, 10);

        canvas.DrawRoundedRectangle(-12, -13, 24, 26, 5);
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(0, 0, 8);
        canvas.FillColor = Color.FromArgb("#79C0FF");
        canvas.FillCircle(0, 0, 3.5f);
    }

    public static void DrawMicrostripLine(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -8, -12, -8);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(12, -8, 20, -8);
        canvas.DrawLine(12, 10, 20, 10);

        // Dielectric substrate
        canvas.FillColor = Color.FromArgb("#21262D");
        canvas.FillRectangle(-12, -3, 24, 11);
        canvas.StrokeColor = Color.FromArgb("#30363D");
        canvas.StrokeSize = 1.2f;
        canvas.DrawRectangle(-12, -3, 24, 11);

        // Top trace
        canvas.FillColor = Color.FromArgb("#56D364");
        canvas.FillRectangle(-12, -8, 24, 5);

        // Ground plane
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-12, 9, 12, 9);
    }

    // ── Power Systems Symbols ──────────────────────────────────────────────

    /// <summary>Single-phase transmission line: π-section schematic symbol.</summary>
    public static void DrawSinglePhaseTransmissionLine(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        // Left and right leads
        canvas.DrawLine(-22, 0, -14, 0);
        canvas.DrawLine(14, 0, 22, 0);
        // Body outline
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-14, -12, 28, 24, 3);
        canvas.DrawRoundedRectangle(-14, -12, 28, 24, 3);
        // Series impedance zigzag inside body
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 1.5f;
        var pts = new float[] { -10, 0, -7, -6, -3, 6, 1, -6, 5, 6, 9, -6, 12, 0 };
        for (int i = 0; i < pts.Length - 2; i += 2)
            canvas.DrawLine(pts[i], pts[i + 1], pts[i + 2], pts[i + 3]);
        // "1φ" text label at bottom of body
        canvas.FontSize = 5.5f;
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString("1φ", -4, 6, 8, 8, HorizontalAlignment.Center, VerticalAlignment.Top);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-22, 0, 2.5f);
        canvas.FillCircle(22, 0, 2.5f);
    }

    /// <summary>Two-phase transmission line with separate A/B phase conductors.</summary>
    public static void DrawTwoPhaseTransmissionLine(ICanvas canvas)
    {
        canvas.StrokeSize = 2f;
        // Phase A leads
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.DrawLine(-22, -10, -14, -10);
        canvas.DrawLine(14, -10, 22, -10);
        // Phase B leads
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.DrawLine(-22, 10, -14, 10);
        canvas.DrawLine(14, 10, 22, 10);
        // Body box
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-14, -18, 28, 36, 3);
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.DrawRoundedRectangle(-14, -18, 28, 36, 3);
        // Phase labels inside box
        canvas.FontSize = 6f;
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString("A", -5, -13, 10, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = Color.FromArgb("#D2A8FF");
        canvas.DrawString("B", -5, 3, 10, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontSize = 5f;
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString("2φ", -4, 10, 8, 8, HorizontalAlignment.Center, VerticalAlignment.Top);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-22, -10, 2.5f);
        canvas.FillCircle(-22, 10, 2.5f);
        canvas.FillCircle(22, -10, 2.5f);
        canvas.FillCircle(22, 10, 2.5f);
    }

    /// <summary>Three-phase transmission line with A/B/C phase conductors.</summary>
    public static void DrawThreePhaseTransmissionLine(ICanvas canvas)
    {
        canvas.StrokeSize = 2f;
        // Phase A leads
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.DrawLine(-22, -14, -14, -14);
        canvas.DrawLine(14, -14, 22, -14);
        // Phase B leads
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.DrawLine(-22, 0, -14, 0);
        canvas.DrawLine(14, 0, 22, 0);
        // Phase C leads
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.DrawLine(-22, 14, -14, 14);
        canvas.DrawLine(14, 14, 22, 14);
        // Body box
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-14, -20, 28, 40, 3);
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.DrawRoundedRectangle(-14, -20, 28, 40, 3);
        // Phase labels
        canvas.FontSize = 6f;
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString("A", -4, -18, 8, 8, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.DrawString("B", -4, -4, 8, 8, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.FontColor = Color.FromArgb("#D2A8FF");
        canvas.DrawString("C", -4, 10, 8, 8, HorizontalAlignment.Center, VerticalAlignment.Center);
        // 3φ label top
        canvas.FontSize = 5f;
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString("3φ", -4, -20, 8, 6, HorizontalAlignment.Center, VerticalAlignment.Top);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-22, -14, 2.5f);
        canvas.FillCircle(-22, 0, 2.5f);
        canvas.FillCircle(-22, 14, 2.5f);
        canvas.FillCircle(22, -14, 2.5f);
        canvas.FillCircle(22, 0, 2.5f);
        canvas.FillCircle(22, 14, 2.5f);
    }

    /// <summary>Synchronous generator: circle with 'G' and phase terminals.</summary>
    public static void DrawSynchronousGenerator(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        // Phase A, B, C leads (left)
        canvas.DrawLine(-22, -14, -14, -14);
        canvas.DrawLine(-22, 0, -14, 0);
        canvas.DrawLine(-22, 14, -14, 14);
        // Neutral lead (right)
        canvas.DrawLine(14, 0, 22, 0);
        // Generator circle
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillCircle(0, 0, 14);
        canvas.DrawCircle(0, 0, 14);
        // 'G' label
        canvas.FontSize = 12f;
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.DrawString("G", -7, -8, 14, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
        // Sine wave accent
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1f;
        canvas.DrawArc(-6, 4, 12, 6, 0, 180, false, false);
        // Dot convention on Phase A
        canvas.FillColor = Color.FromArgb("#E3B341");
        canvas.FillCircle(-14, -14, 2f);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-22, -14, 2.5f);
        canvas.FillCircle(-22, 0, 2.5f);
        canvas.FillCircle(-22, 14, 2.5f);
        canvas.FillCircle(22, 0, 2.5f);
    }

    /// <summary>Induction motor: double-ring circle with 'M' and phase terminals.</summary>
    public static void DrawInductionMotor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#FFA657");
        canvas.StrokeSize = 2f;
        // Phase leads left
        canvas.DrawLine(-22, -14, -14, -14);
        canvas.DrawLine(-22, 0, -14, 0);
        canvas.DrawLine(-22, 14, -14, 14);
        // Neutral lead right
        canvas.DrawLine(14, 0, 22, 0);
        // Outer motor circle
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillCircle(0, 0, 14);
        canvas.DrawCircle(0, 0, 14);
        // Inner rotor ring
        canvas.StrokeSize = 0.75f;
        canvas.StrokeColor = Color.FromArgb("#FFA657");
        canvas.DrawCircle(0, 0, 10);
        canvas.StrokeSize = 2f;
        // 'M' label
        canvas.FontSize = 12f;
        canvas.FontColor = Color.FromArgb("#FFA657");
        canvas.DrawString("M", -7, -8, 14, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-22, -14, 2.5f);
        canvas.FillCircle(-22, 0, 2.5f);
        canvas.FillCircle(-22, 14, 2.5f);
        canvas.FillCircle(22, 0, 2.5f);
    }

    /// <summary>AC bulb: circle body with filament cross, AC tilde, and lead wires.</summary>
    public static void DrawAcBulb(ICanvas canvas, bool isOn)
    {
        var bodyColor = isOn ? Color.FromArgb("#E3B341") : Color.FromArgb("#8B949E");
        canvas.StrokeColor = bodyColor;
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-22, 0, -12, 0);
        canvas.DrawLine(12, 0, 22, 0);
        // Bulb circle
        canvas.FillColor = isOn ? Color.FromArgb("#1A1200") : Color.FromArgb("#161B22");
        canvas.FillCircle(0, 0, 12);
        canvas.DrawCircle(0, 0, 12);
        // Filament X cross
        canvas.StrokeSize = 1.75f;
        canvas.DrawLine(-6, -6, 6, 6);
        canvas.DrawLine(6, -6, -6, 6);
        // AC ~ symbol below
        canvas.StrokeSize = 1.25f;
        canvas.DrawArc(-5, 7, 10, 5, 0, 180, false, false);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-22, 0, 2.5f);
        canvas.FillCircle(22, 0, 2.5f);
    }

    /// <summary>AC fan: circle with 4-blade rotor, hub, and lead wires.</summary>
    public static void DrawAcFan(ICanvas canvas, bool isOn)
    {
        var color = isOn ? Color.FromArgb("#79C0FF") : Color.FromArgb("#8B949E");
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-22, 0, -12, 0);
        canvas.DrawLine(12, 0, 22, 0);
        // Fan circle
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillCircle(0, 0, 12);
        canvas.DrawCircle(0, 0, 12);
        // Four blades as arcs around center
        canvas.FillColor = color;
        canvas.StrokeSize = 0f;
        // Blade 1 (top)
        canvas.DrawArc(-4, -11, 8, 11, 0, 180, true, false);
        // Blade 2 (right)
        canvas.DrawArc(3, -4, 11, 8, 90, 270, true, false);
        // Blade 3 (bottom)
        canvas.DrawArc(-4, 0, 8, 11, 180, 360, true, false);
        // Blade 4 (left)
        canvas.DrawArc(-14, -4, 11, 8, 270, 90, true, false);
        // Hub circle
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.StrokeColor = color;
        canvas.StrokeSize = 1.5f;
        canvas.FillCircle(0, 0, 3);
        canvas.DrawCircle(0, 0, 3);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(-22, 0, 2.5f);
        canvas.FillCircle(22, 0, 2.5f);
    }

    /// <summary>Solar panel: rectangular grid body with sun icon and DC terminals.</summary>
    public static void DrawSolarPanel(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 2f;
        // Top (+) and bottom (-) leads
        canvas.DrawLine(0, -20, 0, -12);
        canvas.DrawLine(0, 12, 0, 20);
        // Panel body
        canvas.FillColor = Color.FromArgb("#0D2137");
        canvas.FillRoundedRectangle(-14, -12, 28, 24, 2);
        canvas.DrawRoundedRectangle(-14, -12, 28, 24, 2);
        // Cell grid lines (3 columns × 2 rows)
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 0.75f;
        canvas.DrawLine(-5, -12, -5, 12);
        canvas.DrawLine(5, -12, 5, 12);
        canvas.DrawLine(-14, -3, 14, -3);
        canvas.DrawLine(-14, 5, 14, 5);
        // + terminal label
        canvas.FontSize = 7f;
        canvas.FontColor = Color.FromArgb("#E3B341");
        canvas.DrawString("+", -3, -20, 6, 8, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("−", -3, 14, 6, 8, HorizontalAlignment.Center, VerticalAlignment.Center);
        // Sun dot top-right corner
        canvas.FillColor = Color.FromArgb("#E3B341");
        canvas.FillCircle(18, -16, 3.5f);
        // Pin dots
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(0, -20, 2.5f);
        canvas.FillCircle(0, 20, 2.5f);
    }

}
