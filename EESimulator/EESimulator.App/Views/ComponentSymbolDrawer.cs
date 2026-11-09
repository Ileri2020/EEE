using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>
/// Provides unified vector drawing methods for schematic component symbols.
/// Used by both the SchematicViewport and sidebar ComponentIconView.
/// </summary>
public static partial class ComponentSymbolDrawer
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

}
