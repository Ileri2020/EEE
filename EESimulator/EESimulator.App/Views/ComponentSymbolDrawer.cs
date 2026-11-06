using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>
/// Provides unified vector drawing methods for schematic component symbols.
/// Used by both the SchematicViewport and sidebar ComponentIconView.
/// </summary>
public static class ComponentSymbolDrawer
{
    public static void DrawSymbol(ICanvas canvas, string componentType, string name = "", bool isOn = false)
    {
        switch (componentType)
        {
            case "Resistor":       DrawResistor(canvas); break;
            case "Capacitor":      DrawCapacitor(canvas); break;
            case "Inductor":       DrawInductor(canvas); break;
            case "VoltageSource":  DrawVoltageSource(canvas); break;
            case "CurrentSource":  DrawCurrentSource(canvas); break;
            case "Ground":         DrawGround(canvas); break;
            case "Diode":          DrawDiode(canvas); break;
            case "LED":            DrawLED(canvas, isOn); break;
            case "Bulb":
            case "Lamp":           DrawBulb(canvas, isOn); break;
            case "LogicInput":
            case "DigitalInput":   DrawLogicInput(canvas, isOn); break;
            case "LogicOutput":
            case "DigitalOutput":  DrawLogicOutput(canvas, isOn); break;
            case "BJT":
            case "NPN":            DrawBJT(canvas, isPnp: false); break;
            case "PNP":            DrawBJT(canvas, isPnp: true); break;
            case "JFET":           DrawJFET(canvas); break;
            case "MOSFET":
            case "NMOS":           DrawMOSFET(canvas, isPmos: false); break;
            case "PMOS":           DrawMOSFET(canvas, isPmos: true); break;
            case "OpAmp":          DrawOpAmp(canvas); break;
            case "VCVS":           DrawVCVS(canvas); break;
            case "VCCS":           DrawVCCS(canvas); break;
            case "CCVS":           DrawCCVS(canvas); break;
            case "CCCS":           DrawCCCS(canvas); break;
            case "VoltageSwitch":  DrawVoltageSwitch(canvas); break;
            case "CurrentSwitch":  DrawCurrentSwitch(canvas); break;
            case "Relay":          DrawRelay(canvas); break;
            case "LogicGate":
            case "AND":            DrawAndGate(canvas); break;
            case "OR":             DrawOrGate(canvas); break;
            case "NOT":            DrawNotGate(canvas); break;
            case "NAND":           DrawNandGate(canvas); break;
            case "NOR":            DrawNorGate(canvas); break;
            case "XOR":            DrawXorGate(canvas); break;
            case "XNOR":           DrawXnorGate(canvas); break;
            case "IC":             DrawIC(canvas); break;
            case "Wire":           DrawWire(canvas); break;
            case "Junction":       DrawJunction(canvas); break;
            case "Label":          DrawLabel(canvas, name); break;
            case "Connector":        DrawConnector(canvas); break;
            case "TextAnnotation":    DrawTextAnnotation(canvas); break;
            case "MutualInductance": DrawMutualInductance(canvas); break;
            case "TransmissionLine": DrawTransmissionLine(canvas); break;
            case "VoltageDelay":     DrawVoltageDelay(canvas); break;
            case "Clock":            DrawClock(canvas, isOn); break;
            case "Buffer":           DrawDigitalBlock(canvas, "BUF", "#58A6FF"); break;
            case "ControlledBuffer": DrawDigitalBlock(canvas, "TRI", "#58A6FF"); break;
            case "EvenParity":       DrawDigitalBlock(canvas, "EVEN", "#58A6FF"); break;
            case "OddParity":        DrawDigitalBlock(canvas, "ODD", "#58A6FF"); break;
            case "PLA":              DrawDigitalBlock(canvas, "PLA", "#58A6FF"); break;
            case "Multiplexer":      DrawDigitalBlock(canvas, "MUX", "#79C0FF"); break;
            case "Demultiplexer":    DrawDigitalBlock(canvas, "DMX", "#79C0FF"); break;
            case "Decoder":          DrawDigitalBlock(canvas, "DEC", "#79C0FF"); break;
            case "PriorityEncoder":  DrawDigitalBlock(canvas, "PRI", "#79C0FF"); break;
            case "BitSelector":      DrawDigitalBlock(canvas, "SEL", "#79C0FF"); break;
            case "DFlipFlop":        DrawDigitalBlock(canvas, "DFF", "#D2A8FF"); break;
            case "JKFlipFlop":       DrawDigitalBlock(canvas, "JK", "#D2A8FF"); break;
            case "SRFlipFlop":       DrawDigitalBlock(canvas, "SR", "#D2A8FF"); break;
            case "TFlipFlop":        DrawDigitalBlock(canvas, "TFF", "#D2A8FF"); break;
            case "Register":         DrawDigitalBlock(canvas, "REG", "#D2A8FF"); break;
            case "Counter":          DrawDigitalBlock(canvas, "CTR", "#D2A8FF"); break;
            case "ShiftRegister":    DrawDigitalBlock(canvas, "SHR", "#D2A8FF"); break;
            case "RAM":              DrawDigitalBlock(canvas, "RAM", "#D2A8FF"); break;
            case "ROM":              DrawDigitalBlock(canvas, "ROM", "#D2A8FF"); break;
            case "Random":           DrawDigitalBlock(canvas, "RND", "#D2A8FF"); break;
            case "Adder":             DrawDigitalBlock(canvas, "+", "#FFB86C"); break;
            case "Subtractor":        DrawDigitalBlock(canvas, "−", "#FFB86C"); break;
            case "Multiplier":        DrawDigitalBlock(canvas, "×", "#FFB86C"); break;
            case "Divider":           DrawDigitalBlock(canvas, "÷", "#FFB86C"); break;
            case "Comparator":        DrawDigitalBlock(canvas, "CMP", "#FFB86C"); break;
            case "Negator":           DrawDigitalBlock(canvas, "−x", "#FFB86C"); break;
            case "Shifter":           DrawDigitalBlock(canvas, "SHF", "#FFB86C"); break;
            case "BitAdder":          DrawDigitalBlock(canvas, "POP", "#FFB86C"); break;
            case "BitFinder":         DrawDigitalBlock(canvas, "FND", "#FFB86C"); break;
            case "SquareRoot":        DrawDigitalBlock(canvas, "√", "#FFB86C"); break;
            case "AbsoluteValue":     DrawDigitalBlock(canvas, "ABS", "#FFB86C"); break;
            case "MinMax":            DrawDigitalBlock(canvas, "MIN", "#FFB86C"); break;
            case "Exponentiator":     DrawDigitalBlock(canvas, "POW", "#FFB86C"); break;
            case "Button":            DrawButton(canvas, isOn); break;
            case "DIPSwitch":         DrawDigitalBlock(canvas, "DIP", "#56D364"); break;
            case "SevenSegment":      DrawSevenSegment(canvas, 0);
                break;
            case "HexDigit":          DrawDigitalBlock(canvas, "HEX", "#56D364"); break;
            case "LedBar":            DrawDigitalBlock(canvas, "LED", "#56D364"); break;
            case "RGBLED":            DrawRgbLed(canvas, 0); break;
            case "DotMatrix":         DrawDigitalBlock(canvas, "DOT", "#56D364"); break;
            case "Joystick":          DrawJoystick(canvas); break;
            case "Keyboard":          DrawDigitalBlock(canvas, "KEY", "#56D364"); break;
            case "TTY":               DrawDigitalBlock(canvas, "TTY", "#56D364"); break;
            case "Video":             DrawDigitalBlock(canvas, "VID", "#56D364"); break;
            case "RealTimeClock":     DrawDigitalBlock(canvas, "RTC", "#56D364"); break;
            case "Pin":              DrawPin(canvas, isOn); break;
            case "Power":
            case "VCC":              DrawPower(canvas); break;
            case "PullResistor":     DrawPullResistor(canvas); break;
            case "Tunnel":           DrawTunnel(canvas, name); break;
            case "Probe":            DrawProbe(canvas, isOn); break;
            case "Transistor":
            case "DigitalTransistor":DrawDigitalTransistor(canvas); break;
            case "TransmissionGate": DrawTransmissionGate(canvas); break;
            case "BitExtender":      DrawBitExtender(canvas); break;
            case "Constant":         DrawConstant(canvas, isOn); break;
            case "DoNotConnect":
            case "DNC":              DrawDoNotConnect(canvas); break;
            case "LossyTransmissionLine": DrawLossyTransmissionLine(canvas); break;
            case "CoaxialCable":     DrawCoaxialCable(canvas); break;
            case "MicrostripLine":   DrawMicrostripLine(canvas); break;
            case "SinglePhaseTransmissionLine": DrawSinglePhaseTransmissionLine(canvas); break;
            case "TwoPhaseTransmissionLine": DrawTwoPhaseTransmissionLine(canvas); break;
            case "ThreePhaseTransmissionLine": DrawThreePhaseTransmissionLine(canvas); break;
            case "ACTransformer": DrawTransformer(canvas); break;
            case "SynchronousGenerator": DrawSynchronousGenerator(canvas); break;
            case "InductionMotor": DrawInductionMotor(canvas); break;
            case "ACBulb": DrawAcBulb(canvas, isOn); break;
            case "ACFan": DrawAcFan(canvas, isOn); break;
            case "SolarPanel": DrawSolarPanel(canvas); break;
            case "Battery": DrawBattery(canvas); break;
            default:                 DrawGenericComponent(canvas, componentType); break;
        }
    }

    public static void DrawTransformer(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 2;
        canvas.DrawLine(-22, -9, -10, -9);
        canvas.DrawLine(-22, 9, -10, 9);
        canvas.DrawLine(10, -9, 22, -9);
        canvas.DrawLine(10, 9, 22, 9);
        canvas.DrawArc(-14, -12, 12, 24, 270, 90, false, false);
        canvas.DrawArc(2, -12, 12, 24, 90, 270, false, false);
        canvas.FillColor = Color.FromArgb("#E3B341");
        canvas.FillCircle(-18, -9, 2);
        canvas.FillCircle(18, -9, 2);
    }

    public static void DrawBattery(ICanvas canvas)
        => DrawBattery(canvas, 0.75);

    public static void DrawBattery(ICanvas canvas, double stateOfChargePercent)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2;
        canvas.DrawRoundedRectangle(-15, -11, 30, 22, 3);
        canvas.FillColor = Color.FromArgb("#56D364");
        canvas.FillRectangle(15, -5, 4, 10);
        float fillWidth = (float)(22 * Math.Clamp(stateOfChargePercent, 0, 100) / 100);
        if (fillWidth > 0) canvas.FillRectangle(-11, -7, fillWidth, 14);
        canvas.DrawLine(-8, 0, -2, 0);
        canvas.DrawLine(-5, -3, -5, 3);
        canvas.DrawLine(3, 0, 9, 0);
    }

    public static float GetDigitalBlockHalfHeight(IEnumerable<ComponentPin>? pins)
    {
        double sidePinExtent = pins?
            .Where(pin => Math.Abs(pin.RelativeX) >= 20)
            .Select(pin => Math.Abs(pin.RelativeY))
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        return (float)Math.Max(13, sidePinExtent - 6);
    }

    public static void DrawDigitalBlock(
        ICanvas canvas,
        string label,
        string color,
        IEnumerable<ComponentPin>? pins = null)
    {
        const float halfWidth = 15f;
        float halfHeight = GetDigitalBlockHalfHeight(pins);
        canvas.StrokeColor = Color.FromArgb(color);
        canvas.StrokeSize = 2f;
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-halfWidth, -halfHeight, halfWidth * 2, halfHeight * 2, 3);
        canvas.DrawRoundedRectangle(-halfWidth, -halfHeight, halfWidth * 2, halfHeight * 2, 3);
        canvas.DrawLine(-22, 0, -halfWidth, 0);
        canvas.DrawLine(halfWidth, 0, 22, 0);
        canvas.FillColor = Color.FromArgb(color);
        canvas.FillCircle(-22, 0, 2);
        canvas.FillCircle(22, 0, 2);
        canvas.FontColor = Color.FromArgb(color);
        canvas.FontSize = label.Length > 3 ? 7f : 9f;
        canvas.DrawString(label, -halfWidth + 2, -5, halfWidth * 2 - 4, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawButton(ICanvas canvas, bool pressed)
    {
        canvas.StrokeColor = pressed ? Color.FromArgb("#56D364") : Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2;
        canvas.DrawLine(-20, 0, -8, 0);
        canvas.DrawLine(8, 0, 20, 0);
        canvas.DrawCircle(0, pressed ? 3 : -2, 8);
        canvas.DrawLine(0, -14, 0, pressed ? -5 : -10);
    }

    public static void DrawSevenSegment(ICanvas canvas, int segments)
    {
        const string colorOn = "#56D364";
        const string colorOff = "#30363D";
        canvas.StrokeSize = 3;
        (float x1, float y1, float x2, float y2)[] lines =
        {
            (-6,-12,6,-12), (8,-10,8,-2), (8,2,8,10), (-6,12,6,12), (-8,2,-8,10), (-8,-10,-8,-2), (-6,0,6,0)
        };
        for (int i = 0; i < lines.Length; i++)
        {
            canvas.StrokeColor = (segments & (1 << i)) != 0 ? Color.FromArgb(colorOn) : Color.FromArgb(colorOff);
            canvas.DrawLine(lines[i].x1, lines[i].y1, lines[i].x2, lines[i].y2);
        }
        var decimalPointColor = (segments & 0x80) != 0 ? Color.FromArgb(colorOn) : Color.FromArgb(colorOff);
        canvas.StrokeColor = decimalPointColor;
        canvas.FillColor = decimalPointColor;
        canvas.FillCircle(12, 10, 2);
    }

    public static void DrawHexDigit(ICanvas canvas, int value)
    {
        int[] patterns = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F, 0x77, 0x7C, 0x39, 0x5E, 0x79, 0x71 };
        DrawSevenSegment(canvas, patterns[Math.Clamp(value, 0, 15)]);
    }

    public static void DrawLedBar(ICanvas canvas, long value, int count)
    {
        canvas.StrokeColor = Color.FromArgb("#30363D");
        canvas.StrokeSize = 1;
        int total = Math.Clamp(count, 1, 16);
        float barWidth = 28f / total;
        for (int bit = 0; bit < total; bit++)
        {
            bool on = ((value >> bit) & 1) != 0;
            canvas.FillColor = on ? Color.FromArgb("#56D364") : Color.FromArgb("#161B22");
            canvas.FillRoundedRectangle(-14 + bit * barWidth, -8, Math.Max(1, barWidth - 1), 16, 1);
            canvas.DrawRoundedRectangle(-14 + bit * barWidth, -8, Math.Max(1, barWidth - 1), 16, 1);
        }
    }

    public static void DrawDotMatrix(ICanvas canvas, long rowMask, long columnMask, int rows, int columns)
    {
        int rowCount = Math.Clamp(rows, 1, 8), columnCount = Math.Clamp(columns, 1, 8);
        float dx = 24f / Math.Max(1, columnCount - 1), dy = 24f / Math.Max(1, rowCount - 1);
        for (int row = 0; row < rowCount; row++)
            for (int column = 0; column < columnCount; column++)
            {
                bool on = ((rowMask >> row) & 1) != 0 && ((columnMask >> column) & 1) != 0;
                canvas.FillColor = on ? Color.FromArgb("#56D364") : Color.FromArgb("#30363D");
                canvas.FillCircle(-12 + column * dx, -12 + row * dy, 1.5f);
            }
    }

    public static void DrawTty(ICanvas canvas, string text)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1.5f;
        canvas.FillColor = Color.FromArgb("#0D1117");
        canvas.FillRoundedRectangle(-18, -12, 36, 24, 3);
        canvas.DrawRoundedRectangle(-18, -12, 36, 24, 3);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 7;
        string visible = text.Length > 6 ? text[^6..] : text;
        canvas.DrawString(visible, -15, -8, 30, 16, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    public static void DrawVideo(ICanvas canvas, int rgb)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2;
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(-19, -14, 38, 28, 3);
        canvas.DrawRoundedRectangle(-19, -14, 38, 28, 3);
        canvas.FillColor = Color.FromArgb($"#{rgb & 0xFFFFFF:X6}");
        canvas.FillRectangle(-15, -10, 30, 20);
        canvas.DrawLine(-7, 14, -7, 19);
        canvas.DrawLine(7, 14, 7, 19);
        canvas.DrawLine(-8, 19, 8, 19);
    }

    public static void DrawRgbLed(ICanvas canvas, int channels)
    {
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2;
        canvas.DrawCircle(0, 0, 13);
        if ((channels & 1) != 0) { canvas.FillColor = Color.FromArgb("#FF5B5B"); canvas.FillCircle(-5, 0, 5); }
        if ((channels & 2) != 0) { canvas.FillColor = Color.FromArgb("#56D364"); canvas.FillCircle(3, -4, 5); }
        if ((channels & 4) != 0) { canvas.FillColor = Color.FromArgb("#58A6FF"); canvas.FillCircle(3, 5, 5); }
        canvas.DrawLine(-6, 13, -6, 20);
        canvas.DrawLine(6, 13, 6, 20);
    }

    public static void DrawJoystick(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2;
        canvas.DrawRoundedRectangle(-14, -14, 28, 28, 6);
        canvas.DrawLine(0, -8, 0, 8);
        canvas.DrawLine(-8, 0, 8, 0);
        canvas.FillColor = Color.FromArgb("#79C0FF");
        canvas.FillCircle(0, 0, 4);
    }

    public static void DrawResistor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#F78166");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -10, 0);
        canvas.DrawLine(10, 0, 20, 0);
        var pts = new[] { -10f, -8f, -6f, 8f, -2f, -8f, 2f, 8f, 6f, -8f, 10f, 0f };
        for (int i = 0; i < pts.Length - 2; i += 2)
            canvas.DrawLine(pts[i], pts[i + 1], pts[i + 2], pts[i + 3]);
    }

    public static void DrawCapacitor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-15, 0, -4, 0);
        canvas.DrawLine(4, 0, 15, 0);
        canvas.DrawLine(-4, -12, -4, 12);
        canvas.DrawLine(4, -12, 4, 12);
    }

    public static void DrawInductor(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -14, 0);
        canvas.DrawLine(14, 0, 20, 0);
        for (int i = -12; i <= 6; i += 6)
        {
            canvas.DrawArc(i, -6, 6, 6, 180, 360, false, false);
        }
    }

    public static void DrawVoltageSource(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(0, 0, 14);
        canvas.DrawLine(0, -20, 0, -14);
        canvas.DrawLine(0, 14, 0, 20);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 11;
        canvas.DrawString("+", -4, -12, 8, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("−", -4, 0, 8, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawGround(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(0, -10, 0, 0);
        canvas.DrawLine(-14, 0, 14, 0);
        canvas.DrawLine(-9, 5, 9, 5);
        canvas.DrawLine(-5, 10, 5, 10);
    }

    public static void DrawCurrentSource(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(0, 0, 14);
        canvas.DrawLine(0, -20, 0, -14);
        canvas.DrawLine(0, 14, 0, 20);
        canvas.DrawLine(0, -7, 0, 7);
        canvas.DrawLine(-4, 2, 0, 7);
        canvas.DrawLine(4, 2, 0, 7);
    }

    public static void DrawDiode(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#FF7B72");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -8, 0);
        canvas.DrawLine(8, 0, 20, 0);
        var path = new PathF();
        path.MoveTo(-8, -10);
        path.LineTo(8, 0);
        path.LineTo(-8, 10);
        path.Close();
        canvas.DrawPath(path);
        canvas.DrawLine(8, -10, 8, 10);
    }

    public static void DrawLED(ICanvas canvas, bool isOn = false)
    {
        if (isOn)
        {
            // Glowing aura behind the diode
            canvas.FillColor = Color.FromRgba(255, 215, 0, 70);
            canvas.FillCircle(0, 0, 15);
        }

        DrawDiode(canvas);

        if (isOn)
        {
            // Illuminated glowing diode triangle fill
            var fillPath = new PathF();
            fillPath.MoveTo(-8, -10);
            fillPath.LineTo(8, 0);
            fillPath.LineTo(-8, 10);
            fillPath.Close();
            canvas.FillColor = Color.FromArgb("#FFD700");
            canvas.FillPath(fillPath);

            // Radiant emission arrows (bright gold)
            canvas.StrokeColor = Color.FromArgb("#FFF275");
            canvas.StrokeSize = 2.0f;
            canvas.DrawLine(3, -12, 11, -19);
            canvas.DrawLine(7, -19, 11, -19);
            canvas.DrawLine(11, -15, 11, -19);

            canvas.DrawLine(8, -8, 16, -15);
            canvas.DrawLine(12, -15, 16, -15);
            canvas.DrawLine(16, -11, 16, -15);
        }
        else
        {
            // Normal / OFF emission arrows
            canvas.StrokeColor = Color.FromArgb("#8B949E");
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(3, -12, 10, -18);
            canvas.DrawLine(7, -18, 10, -18);
            canvas.DrawLine(10, -15, 10, -18);

            canvas.DrawLine(8, -8, 15, -14);
            canvas.DrawLine(12, -14, 15, -14);
            canvas.DrawLine(15, -11, 15, -14);
        }
    }

    public static void DrawBulb(ICanvas canvas, bool isOn = false)
    {
        // Terminal connection leads
        canvas.StrokeColor = isOn ? Color.FromArgb("#F59E0B") : Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -12, 0);
        canvas.DrawLine(12, 0, 20, 0);

        if (isOn)
        {
            // Radiant glow halo
            canvas.FillColor = Color.FromRgba(245, 158, 11, 75);
            canvas.FillCircle(0, 0, 18);

            // Radiant light rays projecting outward around the glass bulb
            canvas.StrokeColor = Color.FromArgb("#FBBF24");
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(0, -14, 0, -20);
            canvas.DrawLine(0, 14, 0, 20);
            canvas.DrawLine(-10, -10, -15, -15);
            canvas.DrawLine(10, -10, 15, -15);
            canvas.DrawLine(-10, 10, -15, 15);
            canvas.DrawLine(10, 10, 15, 15);

            // Inner bulb filled with golden light
            canvas.FillColor = Color.FromRgba(255, 243, 199, 220);
            canvas.FillCircle(0, 0, 12);

            canvas.StrokeColor = Color.FromArgb("#F59E0B");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(0, 0, 12);

            // Glowing tungsten filament cross/coils
            canvas.StrokeColor = Color.FromArgb("#B45309");
            canvas.StrokeSize = 2f;
            canvas.DrawLine(-8, -8, 8, 8);
            canvas.DrawLine(-8, 8, 8, -8);
        }
        else
        {
            // Glass bulb outline
            canvas.StrokeColor = Color.FromArgb("#8B949E");
            canvas.StrokeSize = 2f;
            canvas.DrawCircle(0, 0, 12);

            // Dim dark filament cross
            canvas.StrokeColor = Color.FromArgb("#484F58");
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(-8, -8, 8, 8);
            canvas.DrawLine(-8, 8, 8, -8);
        }
    }

    public static void DrawLogicInput(ICanvas canvas, bool state = false)
    {
        // Output pin lead on the right
        canvas.StrokeColor = state ? Color.FromArgb("#56D364") : Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(12, 0, 20, 0);

        // Toggle badge rectangle
        float bx = -16f, by = -12f, bw = 28f, bh = 24f;
        if (state)
        {
            // Active Logic 1 state (emerald glow)
            canvas.FillColor = Color.FromArgb("#163821");
            canvas.FillRoundedRectangle(bx, by, bw, bh, 5);
            canvas.StrokeColor = Color.FromArgb("#56D364");
            canvas.StrokeSize = 2f;
            canvas.DrawRoundedRectangle(bx, by, bw, bh, 5);

            // Toggle switch thumb (right position)
            canvas.FillColor = Color.FromArgb("#56D364");
            canvas.FillCircle(6, 0, 4);

            // Display bold '1'
            canvas.FontColor = Color.FromArgb("#56D364");
            canvas.FontSize = 13;
            canvas.DrawString("1", bx + 2, by, 14, bh, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        else
        {
            // Inactive Logic 0 state (dim slate)
            canvas.FillColor = Color.FromArgb("#161B22");
            canvas.FillRoundedRectangle(bx, by, bw, bh, 5);
            canvas.StrokeColor = Color.FromArgb("#484F58");
            canvas.StrokeSize = 1.8f;
            canvas.DrawRoundedRectangle(bx, by, bw, bh, 5);

            // Toggle switch thumb (left position)
            canvas.FillColor = Color.FromArgb("#8B949E");
            canvas.FillCircle(-10, 0, 4);

            // Display '0'
            canvas.FontColor = Color.FromArgb("#8B949E");
            canvas.FontSize = 13;
            canvas.DrawString("0", bx + 12, by, 14, bh, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    public static void DrawLogicOutput(ICanvas canvas, bool isOn = false)
    {
        // Input pin lead on the left
        canvas.StrokeColor = isOn ? Color.FromArgb("#79C0FF") : Color.FromArgb("#8B949E");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -12, 0);

        // Indicator badge
        float bx = -12f, by = -12f, bw = 26f, bh = 24f;
        if (isOn)
        {
            // Logic 1 / High probe state
            canvas.FillColor = Color.FromArgb("#0D2D44");
            canvas.FillRoundedRectangle(bx, by, bw, bh, 6);
            canvas.StrokeColor = Color.FromArgb("#79C0FF");
            canvas.StrokeSize = 2f;
            canvas.DrawRoundedRectangle(bx, by, bw, bh, 6);

            // Beacon glow dot
            canvas.FillColor = Color.FromArgb("#56D364");
            canvas.FillCircle(bx + bw - 5, by + 5, 2.5f);

            // Bold '1' indicator
            canvas.FontColor = Color.FromArgb("#79C0FF");
            canvas.FontSize = 13;
            canvas.DrawString("1", bx, by, bw - 4, bh, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        else
        {
            // Logic 0 / Low probe state
            canvas.FillColor = Color.FromArgb("#161B22");
            canvas.FillRoundedRectangle(bx, by, bw, bh, 6);
            canvas.StrokeColor = Color.FromArgb("#30363D");
            canvas.StrokeSize = 1.8f;
            canvas.DrawRoundedRectangle(bx, by, bw, bh, 6);

            // Dim '0' indicator
            canvas.FontColor = Color.FromArgb("#8B949E");
            canvas.FontSize = 13;
            canvas.DrawString("0", bx, by, bw, bh, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    public static void DrawBJT(ICanvas canvas, bool isPnp = false)
    {
        canvas.StrokeColor = Color.FromArgb("#FFA657");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -6, 0);
        canvas.DrawLine(-6, -14, -6, 14);
        canvas.DrawLine(-6, -7, 15, -20);
        canvas.DrawLine(-6, 7, 15, 20);
        if (!isPnp)
        {
            // NPN: Arrow points outward away from base
            canvas.DrawLine(7, 16, 15, 20);
            canvas.DrawLine(11, 10, 15, 20);
        }
        else
        {
            // PNP: Arrow points inward towards base
            canvas.DrawLine(2, 6, -6, 7);
            canvas.DrawLine(-2, 14, -6, 7);
        }
    }

    public static void DrawMOSFET(ICanvas canvas, bool isPmos = false)
    {
        canvas.StrokeColor = Color.FromArgb("#D2A8FF");
        canvas.StrokeSize = 2f;
        if (isPmos)
        {
            // Gate lead with inversion bubble
            canvas.DrawLine(-20, 0, -11, 0);
            canvas.DrawCircle(-8.5f, 0, 2.5f);
            canvas.DrawLine(-6, -14, -6, 14);
            // Channel plate segments
            canvas.DrawLine(-1, -14, -1, -6);
            canvas.DrawLine(-1, -4, -1, 4);
            canvas.DrawLine(-1, 6, -1, 14);
            // Drain and Source
            canvas.DrawLine(-1, -10, 15, -20);
            canvas.DrawLine(-1, 10, 15, 20);
            // Substrate lead with outward arrow (away from channel towards source)
            canvas.DrawLine(-1, 0, 15, 0);
            canvas.DrawLine(15, 0, 15, 20);
            canvas.DrawLine(6, -4, 11, 0);
            canvas.DrawLine(6, 4, 11, 0);
        }
        else
        {
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
            // Substrate lead with inward arrow (towards channel)
            canvas.DrawLine(15, 0, -3, 0);
            canvas.DrawLine(15, 0, 15, 20);
            canvas.DrawLine(3, -4, -3, 0);
            canvas.DrawLine(3, 4, -3, 0);
        }
    }

    public static void DrawOpAmp(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#79C0FF");
        canvas.StrokeSize = 2f;
        var path = new PathF();
        path.MoveTo(-25, -20);
        path.LineTo(20, 0);
        path.LineTo(-25, 20);
        path.Close();
        canvas.DrawPath(path);
        canvas.DrawLine(20, 0, 25, 0);
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.FontSize = 10;
        canvas.DrawString("−", -22, -16, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawString("+", -22, 6, 8, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    public static void DrawAndGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(-12, -16, -12, 16);
        canvas.DrawLine(-12, -16, 0, -16);
        canvas.DrawLine(-12, 16, 0, 16);
        canvas.DrawArc(-16, -16, 32, 32, 270, 90, false, false);
        canvas.DrawLine(16, 0, 20, 0);
    }

    public static void DrawOrGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -10, -10);
        canvas.DrawLine(-20, 10, -10, 10);
        canvas.DrawArc(-26, -16, 26, 32, 270, 90, false, false);
        canvas.DrawLine(-13, -16, 2, -12);
        canvas.DrawLine(2, -12, 16, 0);
        canvas.DrawLine(-13, 16, 2, 12);
        canvas.DrawLine(2, 12, 16, 0);
        canvas.DrawLine(16, 0, 20, 0);
    }

    public static void DrawNotGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, 0, -10, 0);
        var path = new PathF();
        path.MoveTo(-10, -14);
        path.LineTo(10, 0);
        path.LineTo(-10, 14);
        path.Close();
        canvas.DrawPath(path);
        canvas.DrawCircle(13, 0, 3);
        canvas.DrawLine(16, 0, 20, 0);
    }

    public static void DrawNandGate(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 2f;
        canvas.DrawLine(-20, -10, -12, -10);
        canvas.DrawLine(-20, 10, -12, 10);
        canvas.DrawLine(-12, -16, -12, 16);
        canvas.DrawLine(-12, -16, -2, -16);
        canvas.DrawLine(-12, 16, -2, 16);
        canvas.DrawArc(-16, -16, 28, 32, 270, 90, false, false);
        canvas.DrawCircle(14, 0, 3);
        canvas.DrawLine(17, 0, 20, 0);
    }

    public static void DrawNorGate(ICanvas canvas)
    {
        DrawOrGate(canvas);
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.DrawCircle(18, 0, 3);
        canvas.DrawLine(21, 0, 25, 0);
    }

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
