using Microsoft.Maui.Graphics;

namespace EESimulator.App.Views;

/// <summary>
/// Provides unified vector drawing methods for schematic component symbols.
/// Used by both the SchematicViewport and sidebar ComponentIconView.
/// </summary>
public static class ComponentSymbolDrawer
{
    public static void DrawSymbol(ICanvas canvas, string componentType, string name = "")
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
            case "LED":            DrawLED(canvas); break;
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
            case "MutualInductance": DrawMutualInductance(canvas); break;
            case "TransmissionLine": DrawTransmissionLine(canvas); break;
            case "VoltageDelay":     DrawVoltageDelay(canvas); break;
            default:                 DrawGenericComponent(canvas, componentType); break;
        }
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

    public static void DrawLED(ICanvas canvas)
    {
        DrawDiode(canvas);
        canvas.StrokeColor = Color.FromArgb("#E3B341");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(3, -12, 10, -18);
        canvas.DrawLine(7, -18, 10, -18);
        canvas.DrawLine(10, -15, 10, -18);

        canvas.DrawLine(8, -8, 15, -14);
        canvas.DrawLine(12, -14, 15, -14);
        canvas.DrawLine(15, -11, 15, -14);
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
}
