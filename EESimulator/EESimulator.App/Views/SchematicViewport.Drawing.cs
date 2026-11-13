using EESimulator.Engine.Simulation;
using Microsoft.Maui.Graphics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

public partial class SchematicViewport
{
    private sealed class CachedWireSegment
    {
        public PointF Start;
        public PointF End;
        public bool Horizontal;
        public List<float> Crossings = new();
    }

    private sealed class CachedWireRoute
    {
        public Guid Id;
        public ComponentNode? Owner;
        public ConnectionNode? Connection;
        public ComponentNode? FromComp;
        public ComponentNode? ToComp;
        public ComponentPin? FromPin;
        public ComponentPin? ToPin;
        public List<PointF> Path = new();
        public List<CachedWireSegment> Segments = new();
        public float TotalLength;
    }

    private readonly List<CachedWireRoute> _cachedWireRoutes = new();
    private bool _isWireGeometryDirty = true;
    private int _cachedCircuitChildCount = -1;

    public void InvalidateWireGeometryCache()
    {
        _isWireGeometryDirty = true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.Translate(_panX, _panY);
        canvas.Scale(_zoom, _zoom);

        DrawGrid(canvas, dirtyRect);
        DrawOriginCross(canvas);

        if (Circuit != null)
        {
            DrawConnections(canvas);
            DrawComponents(canvas, dirtyRect);
            DrawWireInProgress(canvas);
        }

        canvas.RestoreState();

        // Draw HUD in screen space (after RestoreState)
        DrawViewportLabel(canvas, dirtyRect);
    }

    private void DrawWireInProgress(ICanvas canvas)
    {
        if (!_isDrawingWire || _wireSourceComp == null || _wireSourcePin == null) return;

        var wireStart = SchematicWireRouter.GetPinWorldPosition(_wireSourceComp, _wireSourcePin);
        float x1 = wireStart.X;
        float y1 = wireStart.Y;
        float x2 = _wireCursorWorld.X;
        float y2 = _wireCursorWorld.Y;

        var targetHit = HitTestPin(_wireCursorWorld.X, _wireCursorWorld.Y);
        if (targetHit != null && (targetHit.Value.Component != _wireSourceComp || targetHit.Value.Pin != _wireSourcePin))
        {
            var targetPoint = SchematicWireRouter.GetPinWorldPosition(targetHit.Value.Component, targetHit.Value.Pin);
            x2 = targetPoint.X;
            y2 = targetPoint.Y;

            // Green target snap halo
            canvas.StrokeColor = Color.FromArgb("#56D364");
            canvas.StrokeSize  = 2.2f;
            canvas.DrawCircle(x2, y2, 8.0f);
        }

        // Active wire line (electric blue dash)
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize  = 2.0f;
        canvas.StrokeDashPattern = new float[] { 5, 3 };
        canvas.DrawLine(x1, y1, x2, y2);
        canvas.StrokeDashPattern = null;

        // Terminal endpoints
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(x1, y1, 4.0f);
        canvas.FillColor = Color.FromArgb("#56D364");
        canvas.FillCircle(x2, y2, 4.0f);
    }

    private void DrawGrid(ICanvas canvas, RectF dirtyRect)
    {
        // Compute visible range in world coords
        float x0 = (-_panX / _zoom);
        float y0 = (-_panY / _zoom);
        float x1 = x0 + dirtyRect.Width / _zoom;
        float y1 = y0 + dirtyRect.Height / _zoom;

        // Snap to grid
        x0 = MathF.Floor(x0 / GridSize) * GridSize;
        y0 = MathF.Floor(y0 / GridSize) * GridSize;

        canvas.StrokeColor = Color.FromArgb("#1E2732");
        canvas.StrokeSize = 0.5f;

        for (float gx = x0; gx < x1 + GridSize; gx += GridSize)
            canvas.DrawLine(gx, y0, gx, y1);
        for (float gy = y0; gy < y1 + GridSize; gy += GridSize)
            canvas.DrawLine(x0, gy, x1, gy);
    }

    private void DrawOriginCross(ICanvas canvas)
    {
        canvas.StrokeColor = Color.FromArgb("#30405A");
        canvas.StrokeSize = 1.0f;
        canvas.DrawLine(-30, 0, 30, 0);
        canvas.DrawLine(0, -30, 0, 30);
    }

    private void EnsureWireRoutesBuilt()
    {
        if (Circuit == null)
        {
            _cachedWireRoutes.Clear();
            _isWireGeometryDirty = false;
            return;
        }

        // Fast structural check: if dirty flag or child count changed, rebuild routes
        int childCount = Circuit.Children.Count;
        if (!_isWireGeometryDirty && childCount == _cachedCircuitChildCount)
            return;

        _cachedCircuitChildCount = childCount;
        _isWireGeometryDirty = false;
        _cachedWireRoutes.Clear();

        var components = Circuit.FindChildren<ComponentNode>().ToList();
        var connections = Circuit.FindChildren<ConnectionNode>().ToList();
        var allPriorPaths = new List<List<PointF>>();

        // 1. Process wire components
        foreach (var wire in components.OfType<WireComponent>())
        {
            var path = SchematicWireRouter.RouteWireComponent(wire, components);
            var route = BuildCachedRoute(wire.Id, wire, null, null, null, null, null, path, allPriorPaths);
            _cachedWireRoutes.Add(route);
            allPriorPaths.Add(path);
        }

        // 2. Process connection nodes
        foreach (var conn in connections)
        {
            var fromComp = Circuit.FindChildById<ComponentNode>(conn.FromComponentId);
            var toComp = Circuit.FindChildById<ComponentNode>(conn.ToComponentId);
            if (fromComp == null || toComp == null) continue;
            var fromPin = fromComp.GetPin(conn.FromPin);
            var toPin = toComp.GetPin(conn.ToPin);
            if (fromPin == null || toPin == null) continue;

            var path = SchematicWireRouter.RouteConnection(fromComp, fromPin, toComp, toPin, components, conn.RoutingPoints);
            var route = BuildCachedRoute(conn.Id, null, conn, fromComp, toComp, fromPin, toPin, path, allPriorPaths);
            _cachedWireRoutes.Add(route);
            allPriorPaths.Add(path);
        }
    }

    private static CachedWireRoute BuildCachedRoute(
        Guid id,
        ComponentNode? owner,
        ConnectionNode? connection,
        ComponentNode? fromComp,
        ComponentNode? toComp,
        ComponentPin? fromPin,
        ComponentPin? toPin,
        List<PointF> path,
        List<List<PointF>> priorPaths)
    {
        var route = new CachedWireRoute
        {
            Id = id,
            Owner = owner,
            Connection = connection,
            FromComp = fromComp,
            ToComp = toComp,
            FromPin = fromPin,
            ToPin = toPin,
            Path = path
        };

        float length = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var start = path[i];
            var end = path[i + 1];
            bool horizontal = MathF.Abs(start.Y - end.Y) < 0.01f;
            var crossings = FindCrossings(start, end, horizontal, priorPaths);
            route.Segments.Add(new CachedWireSegment
            {
                Start = start,
                End = end,
                Horizontal = horizontal,
                Crossings = crossings
            });
            length += MathF.Abs(end.X - start.X) + MathF.Abs(end.Y - start.Y);
        }
        route.TotalLength = length;
        return route;
    }

    private void DrawConnections(ICanvas canvas)
    {
        if (Circuit == null) return;
        EnsureWireRoutesBuilt();

        foreach (var route in _cachedWireRoutes)
        {
            DrawCachedWireRoute(canvas, route);

            if (route.Connection == _selectedConnection)
            {
                canvas.StrokeColor = Color.FromArgb("#D2A8FF");
                canvas.StrokeSize = 4f;
                for (int segment = 0; segment < route.Path.Count - 1; segment++)
                    canvas.DrawLine(route.Path[segment], route.Path[segment + 1]);
            }

            int direction = 0;
            float speed = 0f;

            if (route.Owner is WireComponent wire)
            {
                double current = SampleWaveformValue(wire.WaveformCurrent, wire.LiveCurrent);
                direction = GetElectronDirection(current);
                double voltage = wire.Pins
                    .Select(pin => Math.Abs(SamplePinVoltage(wire, pin)))
                    .DefaultIfEmpty()
                    .Max();
                speed = CalculateFlowSpeed(Math.Abs(voltage), Math.Abs(current));

                if (wire.ShowLiveProbe || LiveMeasurementsVisible)
                    DrawWireMeasurement(canvas, wire, route.Path);
            }
            else if (route.Connection != null && route.FromComp != null && route.ToComp != null && route.FromPin != null && route.ToPin != null)
            {
                double fromCurrent = SampleWaveformValue(route.FromComp.WaveformCurrent, route.FromComp.LiveCurrent);
                double toCurrent = SampleWaveformValue(route.ToComp.WaveformCurrent, route.ToComp.LiveCurrent);
                double fromPinCurrent = SamplePinCurrent(route.FromComp, route.FromPin, fromCurrent);
                double toPinCurrent = SamplePinCurrent(route.ToComp, route.ToPin, toCurrent);
                direction = GetElectronDirection(fromPinCurrent, toPinCurrent);
                speed = CalculateFlowSpeed(
                    Math.Abs(SampleWaveformValue(route.FromComp.WaveformVoltage, route.FromComp.LiveVoltage) -
                             SampleWaveformValue(route.ToComp.WaveformVoltage, route.ToComp.LiveVoltage)),
                    Math.Max(Math.Abs(fromPinCurrent), Math.Abs(toPinCurrent)));
            }

            if (IsSimulationPlaying && direction != 0 && speed > 0.01f && route.TotalLength > 1f)
                DrawElectricityDots(canvas, route.Path, direction, speed, route.TotalLength);
        }
    }

    private static float CalculateFlowSpeed(double voltageGap, double branchCurrent)
    {
        if (branchCurrent <= 1e-12)
            return 0;

        float currentFactor = (float)Math.Log10(1 + branchCurrent * 1000);
        float voltageFactor = Math.Min(1, (float)Math.Log10(1 + voltageGap) / 3);
        return Math.Clamp(currentFactor * (0.75f + voltageFactor * 0.25f), 0.015f, 1.8f);
    }

    private static int GetElectronDirection(double current) =>
        Math.Abs(current) <= 1e-12 ? 0 : current > 0 ? -1 : 1;

    private static int GetElectronDirection(double fromPinCurrent, double toPinCurrent)
    {
        if (Math.Abs(fromPinCurrent) > 1e-12)
            return Math.Sign(fromPinCurrent);
        if (Math.Abs(toPinCurrent) > 1e-12)
            return -Math.Sign(toPinCurrent);
        return 0;
    }

    private double SamplePinCurrent(ComponentNode component, ComponentPin pin, double fallback)
    {
        if (component.WaveformPinCurrents.TryGetValue(pin.Id, out var samples))
            return SampleWaveformValue(samples, fallback);

        int pinIndex = component.Pins.IndexOf(pin);
        double branchCurrent = SampleWaveformValue(component.WaveformCurrent, component.LiveCurrent);
        return pinIndex == 1 ? -branchCurrent : branchCurrent;
    }

    private double SampleWaveformValue(List<double> samples, double fallback)
    {
        if (samples.Count == 0) return fallback;
        int index = Math.Clamp((int)(ElectricityAnimPhase * samples.Count), 0, samples.Count - 1);
        return samples[index];
    }

    private void DrawCachedWireRoute(ICanvas canvas, CachedWireRoute route)
    {
        Color routeColor = GetWireRouteColor(route);
        canvas.StrokeColor = routeColor;
        canvas.StrokeSize = 1.7f;

        bool renderBridges = SimulationPerformanceProfile.Current.EnableWireBridges;

        foreach (var seg in route.Segments)
        {
            var start = seg.Start;
            var end = seg.End;
            bool horizontal = seg.Horizontal;

            if (!renderBridges || seg.Crossings.Count == 0)
            {
                canvas.DrawLine(start.X, start.Y, end.X, end.Y);
                continue;
            }

            float cursor = horizontal ? Math.Min(start.X, end.X) : Math.Min(start.Y, end.Y);
            float finish = horizontal ? Math.Max(start.X, end.X) : Math.Max(start.Y, end.Y);

            foreach (float crossing in seg.Crossings)
            {
                const float radius = 7f;
                float clearStart = Math.Max(cursor, crossing - radius);
                float clearEnd = Math.Min(finish, crossing + radius);
                DrawAxisLine(canvas, horizontal, start, cursor, clearStart);
                canvas.StrokeColor = Color.FromArgb("#0D1117");
                canvas.StrokeSize = 4f;
                DrawAxisLine(canvas, horizontal, start, clearStart, clearEnd);
                canvas.StrokeColor = routeColor;
                canvas.StrokeSize = 1.7f;
                DrawBridge(canvas, horizontal, start, crossing, radius);
                cursor = clearEnd;
            }
            DrawAxisLine(canvas, horizontal, start, cursor, finish);
        }

        if (route.Path.Count > 0)
        {
            canvas.FillColor = routeColor;
            canvas.FillCircle(route.Path[0].X, route.Path[0].Y, 3f);
            canvas.FillCircle(route.Path[^1].X, route.Path[^1].Y, 3f);
        }
    }

    private Color GetWireRouteColor(CachedWireRoute route)
    {
        if (route.Owner is WireComponent wire)
        {
            double voltage = wire.Pins
                .Select(pin => Math.Abs(SamplePinVoltage(wire, pin)))
                .DefaultIfEmpty(0)
                .Max();
            double current = Math.Abs(SampleWaveformValue(wire.WaveformCurrent, wire.LiveCurrent));
            return GetElectricalStateColor(voltage, current);
        }

        if (route.FromComp == null || route.ToComp == null ||
            route.FromPin == null || route.ToPin == null)
            return Color.FromArgb("#6E7681");

        double fromVoltage = Math.Abs(SamplePinVoltage(route.FromComp, route.FromPin));
        double toVoltage = Math.Abs(SamplePinVoltage(route.ToComp, route.ToPin));
        double fromCurrent = Math.Abs(SamplePinCurrent(route.FromComp, route.FromPin, route.FromComp.LiveCurrent));
        double toCurrent = Math.Abs(SamplePinCurrent(route.ToComp, route.ToPin, route.ToComp.LiveCurrent));
        return GetElectricalStateColor(Math.Max(fromVoltage, toVoltage), Math.Max(fromCurrent, toCurrent));
    }

    private static Color GetElectricalStateColor(double voltage, double current)
    {
        // 1. Conduction: active branch current flowing through wire/node
        if (current >= 1e-6)
            return Color.FromArgb("#3FB950"); // Vivid energized green for active current flow

        // 2. Logic level / potential present with negligible current (CMOS gate inputs, logic lines)
        if (voltage >= 0.5)
            return Color.FromArgb("#58A6FF"); // Electric cyan-blue for logic high / potential presence

        // 3. Intermediate / weak voltage state (0.05V to 0.5V)
        if (voltage >= 0.05)
            return Color.FromArgb("#D2A8FF"); // Lavender purple for floating/intermediate potential

        // 4. Ground / unenergized / zero state (0V and 0A)
        return Color.FromArgb("#6E7681"); // Muted slate gray for ground and inactive nets
    }

    private double SamplePinVoltage(ComponentNode component, ComponentPin pin)
    {
        double liveVoltage = component.LivePinVoltages.TryGetValue(pin.Id, out var voltage) ? voltage : 0.0;
        return component.WaveformNodeVoltages.TryGetValue(pin.Id, out var samples)
            ? SampleWaveformValue(samples, liveVoltage)
            : liveVoltage;
    }

    private static List<float> FindCrossings(PointF start, PointF end, bool horizontal, List<List<PointF>> previousPaths)
    {
        var crossings = new List<float>();
        foreach (var prior in previousPaths)
        {
            for (int i = 0; i < prior.Count - 1; i++)
            {
                var a = prior[i];
                var b = prior[i + 1];
                bool priorHorizontal = MathF.Abs(a.Y - b.Y) < 0.01f;
                if (priorHorizontal == horizontal) continue;
                float x = horizontal ? a.X : start.X;
                float y = horizontal ? start.Y : a.Y;
                float currentMin = horizontal ? Math.Min(start.X, end.X) : Math.Min(start.Y, end.Y);
                float currentMax = horizontal ? Math.Max(start.X, end.X) : Math.Max(start.Y, end.Y);
                float priorMin = priorHorizontal ? Math.Min(a.X, b.X) : Math.Min(a.Y, b.Y);
                float priorMax = priorHorizontal ? Math.Max(a.X, b.X) : Math.Max(a.Y, b.Y);
                float alongCurrent = horizontal ? x : y;
                float alongPrior = priorHorizontal ? x : y;
                if (alongCurrent <= currentMin + 8 || alongCurrent >= currentMax - 8 ||
                    alongPrior <= priorMin + 3 || alongPrior >= priorMax - 3) continue;
                crossings.Add(alongCurrent);
            }
        }
        return crossings.Distinct().OrderBy(value => value).ToList();
    }

    private static void DrawAxisLine(ICanvas canvas, bool horizontal, PointF reference, float from, float to)
    {
        if (to <= from) return;
        if (horizontal) canvas.DrawLine(from, reference.Y, to, reference.Y);
        else canvas.DrawLine(reference.X, from, reference.X, to);
    }

    private static void DrawBridge(ICanvas canvas, bool horizontal, PointF reference, float center, float radius)
    {
        var bridge = new PathF();
        if (horizontal)
        {
            bridge.MoveTo(center - radius, reference.Y);
            bridge.LineTo(center - radius, reference.Y - 4);
            bridge.LineTo(center - radius * 0.5f, reference.Y - 7);
            bridge.LineTo(center + radius * 0.5f, reference.Y - 7);
            bridge.LineTo(center + radius, reference.Y - 4);
            bridge.LineTo(center + radius, reference.Y);
        }
        else
        {
            bridge.MoveTo(reference.X, center - radius);
            bridge.LineTo(reference.X + 4, center - radius);
            bridge.LineTo(reference.X + 7, center - radius * 0.5f);
            bridge.LineTo(reference.X + 7, center + radius * 0.5f);
            bridge.LineTo(reference.X + 4, center + radius);
            bridge.LineTo(reference.X, center + radius);
        }
        canvas.DrawPath(bridge);
    }

    private void DrawElectricityDots(ICanvas canvas, List<PointF> path, int electronDirection, float flowSpeed, float totalLength)
    {
        if (totalLength < 1f || path.Count < 2) return;

        float density = SimulationPerformanceProfile.Current.ParticleDensityMultiplier;
        int dotCount = Math.Clamp((int)((totalLength / 28f) * density), 1, 6);
        for (int i = 0; i < dotCount; i++)
        {
            float norm = ((float)i / dotCount + ElectricityAnimPhase * flowSpeed) % 1.0f;
            if (electronDirection < 0) norm = 1f - norm;
            float distance = norm * totalLength;
            for (int segment = 0; segment < path.Count - 1; segment++)
            {
                var a = path[segment];
                var b = path[segment + 1];
                float segmentLength = MathF.Abs(b.X - a.X) + MathF.Abs(b.Y - a.Y);
                if (distance > segmentLength) { distance -= segmentLength; continue; }
                float ratio = segmentLength > 0 ? distance / segmentLength : 0;
                float x = a.X + (b.X - a.X) * ratio;
                float y = a.Y + (b.Y - a.Y) * ratio;
                canvas.FillColor = Color.FromArgb("#6656D364");
                canvas.FillCircle(x, y, 4f);
                canvas.FillColor = Color.FromArgb("#E3B341");
                canvas.FillCircle(x, y, 2.2f);
                break;
            }
        }
    }

    private static void DrawWireMeasurement(ICanvas canvas, WireComponent wire, List<PointF> path)
    {
        if (path.Count < 2) return;
        var midpoint = path[path.Count / 2];
        canvas.FillColor = Color.FromArgb("#161B22");
        canvas.FillRoundedRectangle(midpoint.X + 4, midpoint.Y - 19, 84, 30, 4);
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(midpoint.X + 4, midpoint.Y - 19, 84, 30, 4);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 8f;
        canvas.DrawString($"{wire.LiveVoltage:F2} V", midpoint.X + 8, midpoint.Y - 17, 76, 11, HorizontalAlignment.Left, VerticalAlignment.Center);
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString($"{wire.LiveCurrent * 1000:F1} mA", midpoint.X + 8, midpoint.Y - 5, 76, 11, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    private void DrawComponents(ICanvas canvas, RectF dirtyRect)
    {
        if (Circuit == null) return;
        _liveMeasurementBadgeBounds.Clear();

        bool enableCulling = SimulationPerformanceProfile.Current.EnableViewportCulling && dirtyRect.Width > 0 && dirtyRect.Height > 0;
        float minVx = 0, maxVx = 0, minVy = 0, maxVy = 0;
        if (enableCulling)
        {
            minVx = (-_panX / _zoom) - 80f;
            maxVx = minVx + (dirtyRect.Width / _zoom) + 160f;
            minVy = (-_panY / _zoom) - 80f;
            maxVy = minVy + (dirtyRect.Height / _zoom) + 160f;
        }

        foreach (var comp in Circuit.FindChildren<ComponentNode>())
        {
            if (comp is WireComponent) continue;

            bool selected = comp == _selectedComponent;
            if (enableCulling && !selected)
            {
                float cx = (float)comp.X;
                float cy = (float)comp.Y;
                if (cx < minVx || cx > maxVx || cy < minVy || cy > maxVy)
                    continue;
            }

            DrawComponent(canvas, comp, selected);
        }
    }

    private void DrawComponent(ICanvas canvas, ComponentNode comp, bool selected)
    {
        float cx = (float)comp.X;
        float cy = (float)comp.Y;

        canvas.SaveState();
        canvas.Translate(cx, cy);
        canvas.Rotate((float)comp.Rotation);

        float sx = (float)(comp.ScaleX > 0 ? comp.ScaleX : 1.0);
        float sy = (float)(comp.ScaleY > 0 ? comp.ScaleY : 1.0);
        AdaptiveComponentLayout layout = AdaptiveComponentLayout.Get(comp);

        // Selection outline
        if (selected)
        {
            float halfX = GetSelectionHalfWidth(comp);
            float halfY = GetSelectionHalfHeight(comp);

            // Glow border
            canvas.StrokeColor = Color.FromArgb("#79C0FF");
            canvas.StrokeSize = 1.5f;
            canvas.StrokeDashPattern = new float[] { 4, 3 };
            canvas.DrawRoundedRectangle(-halfX, -halfY, halfX * 2, halfY * 2, 4);
            canvas.StrokeDashPattern = null;
        }

        bool isBroken = comp.IsBrokenDown;
        if (isBroken)
        {
            // Glowing red breakdown / overload halo behind the component
            canvas.FillColor = Color.FromRgba(248, 81, 73, 90);
            canvas.FillCircle(0, 0, (float)Math.Max(layout.HalfWidth, layout.HalfHeight));

            // Breakdown warning indicator badge at top-right
            canvas.FillColor = Color.FromArgb("#B3261E");
            canvas.FillCircle((float)layout.HalfWidth, (float)-layout.HalfHeight, 8.5f);
            canvas.FontColor = Colors.White;
            canvas.FontSize = 9f;
            canvas.DrawString("⚡", (float)layout.HalfWidth - 5, (float)-layout.HalfHeight - 5, 10, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        bool isOn = comp switch
        {
            EESimulator.Core.Components.Electronics.LEDComponent led => led.IsOn,
            EESimulator.Core.Components.Electrical.BulbComponent bulb => bulb.IsOn,
            EESimulator.Core.Components.Electrical.ACBulbComponent acBulb => acBulb.InitiallyOn && acBulb.ScheduleActive,
            EESimulator.Core.Components.Electronics.LogicOutputComponent lout => lout.IsOn,
            EESimulator.Core.Components.Electronics.LogicInputComponent lin => lin.State,
            EESimulator.Core.Components.Electronics.ClockComponent clk => clk.State,
            EESimulator.Core.Components.Electronics.PinComponent pin => pin.Value > 0,
            EESimulator.Core.Components.Electronics.ConstantComponent cst => cst.Value > 0,
            EESimulator.Core.Components.Electronics.ButtonComponent button => button.IsPressed,
            _ => (comp.CustomProperties.TryGetValue("IsOn", out var cVal) && Convert.ToBoolean(cVal))
                 || (comp.ElectricalParameters.TryGetValue("IsOn", out var pVal) && pVal > 0.5)
                 || (comp.ComponentType == "LED" && comp.LiveVoltage >= 1.5)
                 || (comp.ComponentType == "Bulb" && comp.LiveVoltage >= 1.0)
        };

        canvas.SaveState();
        canvas.Scale(sx * (float)layout.ScaleFactor, sy * (float)layout.ScaleFactor);

        switch (comp.ComponentType)
        {
            case "Resistor":      DrawResistor(canvas); break;
            case "Capacitor":     DrawCapacitor(canvas); break;
            case "Inductor":      DrawInductor(canvas); break;
            case "VoltageSource": DrawVoltageSource(canvas); break;
            case "CurrentSource": DrawCurrentSource(canvas); break;
            case "Ground":        DrawGround(canvas); break;
            case "Diode":         DrawDiode(canvas); break;
            case "LED":           ComponentSymbolDrawer.DrawLED(canvas, isOn); break;
            case "Bulb":
            case "Lamp":          ComponentSymbolDrawer.DrawBulb(canvas, isOn); break;
            case "LogicInput":
            case "DigitalInput":  ComponentSymbolDrawer.DrawLogicInput(canvas, isOn); break;
            case "LogicOutput":
            case "DigitalOutput": ComponentSymbolDrawer.DrawLogicOutput(canvas, isOn); break;
            case "BJT":
            case "NPN":
            case "PNP":
                bool isPnp = (comp is EESimulator.Core.Components.Electronics.BJTComponent bjt && bjt.Polarity == EESimulator.Core.Components.Electronics.BjtPolarity.PNP) || comp.ComponentType.Equals("PNP", StringComparison.OrdinalIgnoreCase);
                ComponentSymbolDrawer.DrawBJT(canvas, isPnp);
                break;
            case "JFET":          ComponentSymbolDrawer.DrawJFET(canvas); break;
            case "MOSFET":
            case "NMOS":
            case "PMOS":
                bool isPmos = (comp is EESimulator.Core.Components.Electronics.MOSFETComponent mos && mos.Channel == EESimulator.Core.Components.Electronics.MosfetChannel.PChannel) || comp.ComponentType.Equals("PMOS", StringComparison.OrdinalIgnoreCase);
                ComponentSymbolDrawer.DrawMOSFET(canvas, isPmos);
                break;
            case "OpAmp":         DrawOpAmp(canvas); break;
            case "VCVS":          ComponentSymbolDrawer.DrawVCVS(canvas); break;
            case "VCCS":          ComponentSymbolDrawer.DrawVCCS(canvas); break;
            case "CCVS":          ComponentSymbolDrawer.DrawCCVS(canvas); break;
            case "CCCS":          ComponentSymbolDrawer.DrawCCCS(canvas); break;
            case "VoltageSwitch": ComponentSymbolDrawer.DrawVoltageSwitch(canvas); break;
            case "CurrentSwitch": ComponentSymbolDrawer.DrawCurrentSwitch(canvas); break;
            case "Relay": ComponentSymbolDrawer.DrawRelay(canvas); break;
            case "LogicGate":
            case "AND":           DrawAndGate(canvas); break;
            case "OR":            DrawOrGate(canvas); break;
            case "NOT":           DrawNotGate(canvas); break;
            case "NAND":          DrawNandGate(canvas); break;
            case "NOR":           DrawNorGate(canvas); break;
            case "XOR":           DrawXorGate(canvas); break;
            case "XNOR":          DrawXnorGate(canvas); break;
            case "IC":            DrawIC(canvas); break;
            case "Wire":           break; // Routed separately so it can avoid components and bridge crossings.
            case "CustomCircuit":
                string macroLabel = comp is CustomCircuitComponent macro
                    ? macro.DisplayName[..Math.Min(5, macro.DisplayName.Length)].ToUpperInvariant()
                    : "MACRO";
                ComponentSymbolDrawer.DrawDigitalBlock(canvas, macroLabel, "#D2A8FF", comp.Pins);
                break;
            case "Junction":      DrawJunction(canvas, comp); break;
            case "Label":            DrawLabel(canvas, comp.Name); break;
            case "MutualInductance": ComponentSymbolDrawer.DrawMutualInductance(canvas); break;
            case "TransmissionLine": ComponentSymbolDrawer.DrawTransmissionLine(canvas); break;
            case "VoltageDelay":     ComponentSymbolDrawer.DrawVoltageDelay(canvas); break;
            case "Clock":            ComponentSymbolDrawer.DrawClock(canvas, isOn); break;
            case "Buffer":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "BUF", "#58A6FF", comp.Pins); break;
            case "ControlledBuffer": ComponentSymbolDrawer.DrawDigitalBlock(canvas, "TRI", "#58A6FF", comp.Pins); break;
            case "EvenParity":       ComponentSymbolDrawer.DrawDigitalBlock(canvas, "EVEN", "#58A6FF", comp.Pins); break;
            case "OddParity":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "ODD", "#58A6FF", comp.Pins); break;
            case "PLA":              ComponentSymbolDrawer.DrawDigitalBlock(canvas, "PLA", "#58A6FF", comp.Pins); break;
            case "Multiplexer":      ComponentSymbolDrawer.DrawDigitalBlock(canvas, "MUX", "#79C0FF", comp.Pins); break;
            case "Demultiplexer":    ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DMX", "#79C0FF", comp.Pins); break;
            case "Decoder":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DEC", "#79C0FF", comp.Pins); break;
            case "PriorityEncoder":  ComponentSymbolDrawer.DrawDigitalBlock(canvas, "PRI", "#79C0FF", comp.Pins); break;
            case "BitSelector":      ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SEL", "#79C0FF", comp.Pins); break;
            case "DFlipFlop":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DFF", "#D2A8FF", comp.Pins); break;
            case "JKFlipFlop":       ComponentSymbolDrawer.DrawDigitalBlock(canvas, "JK", "#D2A8FF", comp.Pins); break;
            case "SRFlipFlop":       ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SR", "#D2A8FF", comp.Pins); break;
            case "TFlipFlop":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "TFF", "#D2A8FF", comp.Pins); break;
            case "Register":         ComponentSymbolDrawer.DrawDigitalBlock(canvas, "REG", "#D2A8FF", comp.Pins); break;
            case "Counter":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "CTR", "#D2A8FF", comp.Pins); break;
            case "ShiftRegister":    ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SHR", "#D2A8FF", comp.Pins); break;
            case "RAM":              ComponentSymbolDrawer.DrawDigitalBlock(canvas, "RAM", "#D2A8FF", comp.Pins); break;
            case "ROM":              ComponentSymbolDrawer.DrawDigitalBlock(canvas, "ROM", "#D2A8FF", comp.Pins); break;
            case "Random":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "RND", "#D2A8FF", comp.Pins); break;
            case "Adder":             ComponentSymbolDrawer.DrawDigitalBlock(canvas, "+", "#FFB86C", comp.Pins); break;
            case "Subtractor":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "−", "#FFB86C", comp.Pins); break;
            case "Multiplier":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "×", "#FFB86C", comp.Pins); break;
            case "Divider":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "÷", "#FFB86C", comp.Pins); break;
            case "Comparator":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "CMP", "#FFB86C", comp.Pins); break;
            case "Negator":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "−x", "#FFB86C", comp.Pins); break;
            case "Shifter":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SHF", "#FFB86C", comp.Pins); break;
            case "BitAdder":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "POP", "#FFB86C", comp.Pins); break;
            case "BitFinder":         ComponentSymbolDrawer.DrawDigitalBlock(canvas, "FND", "#FFB86C", comp.Pins); break;
            case "SquareRoot":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "√", "#FFB86C", comp.Pins); break;
            case "AbsoluteValue":     ComponentSymbolDrawer.DrawDigitalBlock(canvas, "ABS", "#FFB86C", comp.Pins); break;
            case "MinMax":            ComponentSymbolDrawer.DrawDigitalBlock(canvas, "MIN", "#FFB86C", comp.Pins); break;
            case "Exponentiator":     ComponentSymbolDrawer.DrawDigitalBlock(canvas, "POW", "#FFB86C", comp.Pins); break;
            case "Button":            ComponentSymbolDrawer.DrawButton(canvas, isOn); break;
            case "DIPSwitch":         ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DIP", "#56D364", comp.Pins); break;
            case "SevenSegment":      ComponentSymbolDrawer.DrawSevenSegment(canvas, comp.CustomProperties.TryGetValue("RenderedSegments", out var segs) ? Convert.ToInt32(segs) : 0); break;
            case "HexDigit":          ComponentSymbolDrawer.DrawHexDigit(canvas, comp.CustomProperties.TryGetValue("RenderedValue", out var hexValue) ? Convert.ToInt32(hexValue) : 0); break;
            case "LedBar":            ComponentSymbolDrawer.DrawLedBar(canvas, comp.CustomProperties.TryGetValue("RenderedValue", out var ledBarValue) ? Convert.ToInt64(ledBarValue) : 0, comp is EESimulator.Core.Components.Electronics.LedBarComponent ledBar ? ledBar.LedCount : 8); break;
            case "RGBLED":            ComponentSymbolDrawer.DrawRgbLed(canvas, comp.CustomProperties.TryGetValue("RenderedRgb", out var rgb) ? Convert.ToInt32(rgb) : 0); break;
            case "DotMatrix":         ComponentSymbolDrawer.DrawDotMatrix(canvas, comp.CustomProperties.TryGetValue("RenderedRows", out var matrixRows) ? Convert.ToInt64(matrixRows) : 0, comp.CustomProperties.TryGetValue("RenderedColumns", out var matrixCols) ? Convert.ToInt64(matrixCols) : 0, comp is EESimulator.Core.Components.Electronics.DotMatrixComponent matrix ? matrix.Rows : 7, comp is EESimulator.Core.Components.Electronics.DotMatrixComponent matrixColumns ? matrixColumns.Columns : 5); break;
            case "Joystick":          ComponentSymbolDrawer.DrawJoystick(canvas); break;
            case "Keyboard":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "KEY", "#56D364", comp.Pins); break;
            case "TTY":               ComponentSymbolDrawer.DrawTty(canvas, comp is EESimulator.Core.Components.Electronics.TtyComponent tty ? tty.Text : string.Empty); break;
            case "Video":             ComponentSymbolDrawer.DrawVideo(canvas, comp.CustomProperties.TryGetValue("RenderedRgb", out var videoRgb) ? Convert.ToInt32(videoRgb) : 0); break;
            case "RealTimeClock":
                string clockLabel = comp.CustomProperties.TryGetValue("RenderedHours", out var clockHours) && comp.CustomProperties.TryGetValue("RenderedMinutes", out var clockMinutes)
                    ? $"{Convert.ToInt32(clockHours):00}:{Convert.ToInt32(clockMinutes):00}"
                    : "RTC";
                ComponentSymbolDrawer.DrawDigitalBlock(canvas, clockLabel, "#56D364", comp.Pins);
                break;
            case "Pin":              ComponentSymbolDrawer.DrawPin(canvas, isOn); break;
            case "Power":
            case "VCC":              ComponentSymbolDrawer.DrawPower(canvas); break;
            case "PullResistor":     ComponentSymbolDrawer.DrawPullResistor(canvas); break;
            case "Tunnel":           ComponentSymbolDrawer.DrawTunnel(canvas, comp.Reference.Length > 0 ? comp.Reference : comp.Name); break;
            case "Probe":            ComponentSymbolDrawer.DrawProbe(canvas, isOn); break;
            case "Transistor":
            case "DigitalTransistor":ComponentSymbolDrawer.DrawDigitalTransistor(canvas); break;
            case "TransmissionGate": ComponentSymbolDrawer.DrawTransmissionGate(canvas); break;
            case "BitExtender":      ComponentSymbolDrawer.DrawBitExtender(canvas); break;
            case "Constant":         ComponentSymbolDrawer.DrawConstant(canvas, isOn); break;
            case "DoNotConnect":
            case "DNC":              ComponentSymbolDrawer.DrawDoNotConnect(canvas); break;
            case "LossyTransmissionLine": ComponentSymbolDrawer.DrawLossyTransmissionLine(canvas); break;
            case "CoaxialCable":     ComponentSymbolDrawer.DrawCoaxialCable(canvas); break;
            case "MicrostripLine":   ComponentSymbolDrawer.DrawMicrostripLine(canvas); break;
            case "SinglePhaseTransmissionLine": ComponentSymbolDrawer.DrawSinglePhaseTransmissionLine(canvas); break;
            case "TwoPhaseTransmissionLine": ComponentSymbolDrawer.DrawTwoPhaseTransmissionLine(canvas); break;
            case "ThreePhaseTransmissionLine": ComponentSymbolDrawer.DrawThreePhaseTransmissionLine(canvas); break;
            case "ACTransformer": ComponentSymbolDrawer.DrawTransformer(canvas); break;
            case "SynchronousGenerator": ComponentSymbolDrawer.DrawDigitalBlock(canvas, "GEN", "#56D364", comp.Pins); break;
            case "InductionMotor": ComponentSymbolDrawer.DrawDigitalBlock(canvas, "MTR", "#FFA657", comp.Pins); break;
            case "ACBulb": ComponentSymbolDrawer.DrawBulb(canvas, isOn); break;
            case "ACFan": ComponentSymbolDrawer.DrawDigitalBlock(canvas, "FAN", "#79C0FF", comp.Pins); break;
            case "SolarPanel": ComponentSymbolDrawer.DrawDigitalBlock(canvas, "PV", "#E3B341", comp.Pins); break;
            case "Battery": ComponentSymbolDrawer.DrawBattery(canvas, comp is EESimulator.Core.Components.Electrical.BatteryComponent battery ? battery.StateOfChargePercent : 0); break;
            default:
                if (comp is EESimulator.Core.Components.Electronics.TtlPackageComponent ttl)
                    ComponentSymbolDrawer.DrawTtlPackage(canvas, ttl.PartNumber, ttl.ShowInternalStructure);
                else
                    DrawGenericComponent(canvas, comp.ComponentType);
                break;
        }

        canvas.RestoreState();

        // Label
        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = 9;
        canvas.DrawString(comp.Reference.Length > 0 ? comp.Reference : comp.Name,
            -GetSelectionHalfWidth(comp), GetSelectionHalfHeight(comp) + 2,
            GetSelectionHalfWidth(comp) * 2, 14, HorizontalAlignment.Center, VerticalAlignment.Top);

        // Waveform indicator (Graph icon enabled)
        if (comp.ShowWaveform)
        {
            canvas.FillColor = Color.FromArgb("#161B22");
            canvas.FillCircle(-22, -18, 9);
            canvas.StrokeColor = Color.FromArgb("#58A6FF");
            canvas.StrokeSize = 1.2f;
            canvas.DrawCircle(-22, -18, 9);
            canvas.FontColor = Color.FromArgb("#58A6FF");
            canvas.FontSize = 8;
            canvas.DrawString("📈", -26, -24, 12, 12, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        // Draw small circle to nodes/pins on all components in viewport with connection degree coloring
        foreach (var pin in comp.Pins)
        {
            ComponentPinPosition position = layout.GetPinPosition(pin);
            float px = (float)position.X;
            float py = (float)position.Y;
            bool isPinSelected = (_selectedPin != null &&
                                  _selectedPin.Value.Component == comp &&
                                  _selectedPin.Value.Pin == pin);

            int connCount = GetPinConnectionCount(comp, pin);
            Color nodeColor = isPinSelected ? Color.FromArgb("#FFFFFF") : GetNodeConnectionColor(connCount);

            // Small circle terminal for connection/selection
            canvas.FillColor = isPinSelected ? Color.FromArgb("#58A6FF") : Color.FromRgba(nodeColor.Red, nodeColor.Green, nodeColor.Blue, 0.35f);
            canvas.FillCircle(px, py, 3.8f);

            canvas.StrokeColor = nodeColor;
            canvas.StrokeSize = isPinSelected ? 2.2f : 1.5f;
            canvas.DrawCircle(px, py, 3.8f);

            // Outer selection halo or degree indicator for connected nodes
            if (isPinSelected)
            {
                canvas.StrokeColor = Color.FromArgb("#58A6FF");
                canvas.StrokeSize = 1.0f;
                canvas.DrawCircle(px, py, 6.2f);
            }
            else if (connCount >= 2)
            {
                canvas.StrokeColor = Color.FromRgba(nodeColor.Red, nodeColor.Green, nodeColor.Blue, 0.45f);
                canvas.StrokeSize = 0.8f;
                canvas.DrawCircle(px, py, 5.5f);
            }
        }

        canvas.RestoreState();

        // Draw measurement badges unrotated and outside the component body. Try
        // nearby positions and skip occupied component, wire, and badge regions.
        if ((comp.ShowLiveProbe || LiveMeasurementsVisible) && comp.ComponentType != "Wire")
            DrawLiveMeasurementBadge(canvas, comp);
    }

    private void DrawLiveMeasurementBadge(ICanvas canvas, ComponentNode component)
    {
        const float width = 84f;
        const float height = 30f;
        var candidates = new[]
        {
            new RectF((float)component.X + 38, (float)component.Y - 34, width, height),
            new RectF((float)component.X - width - 38, (float)component.Y - 34, width, height),
            new RectF((float)component.X - width / 2, (float)component.Y - 66, width, height),
            new RectF((float)component.X - width / 2, (float)component.Y + 38, width, height),
            new RectF((float)component.X + 38, (float)component.Y + 38, width, height),
            new RectF((float)component.X - width - 38, (float)component.Y + 38, width, height)
        };

        var components = Circuit?.FindChildren<ComponentNode>().ToList() ?? new List<ComponentNode>();
        RectF badge = candidates.FirstOrDefault(candidate => !IsMeasurementBadgeBlocked(candidate, component, components));
        if (badge.Width <= 0 || badge.Height <= 0)
            badge = candidates[0];
        _liveMeasurementBadgeBounds.Add(badge);

        canvas.SaveState();
        canvas.Translate(badge.Left, badge.Top);
        canvas.FillColor = Color.FromArgb("#E6161B22");
        canvas.FillRoundedRectangle(0, 0, width, height, 4);
        canvas.StrokeColor = Color.FromArgb("#56D364");
        canvas.StrokeSize = 1.2f;
        canvas.DrawRoundedRectangle(0, 0, width, height, 4);
        canvas.FontColor = Color.FromArgb("#56D364");
        canvas.FontSize = 8.5f;
        canvas.DrawString($"👁 {component.LiveVoltage:F2} V", 4, 2, 76, 12, HorizontalAlignment.Left, VerticalAlignment.Center);
        canvas.FontColor = Color.FromArgb("#79C0FF");
        canvas.DrawString($"{component.LiveCurrent * 1000:F1} mA", 4, 15, 76, 12, HorizontalAlignment.Left, VerticalAlignment.Center);
        canvas.RestoreState();
    }

    private bool IsMeasurementBadgeBlocked(RectF badge, ComponentNode target, List<ComponentNode> components)
    {
        if (_liveMeasurementBadgeBounds.Any(existing => RectanglesOverlap(existing, badge)))
            return true;

        foreach (var component in components)
        {
            if (ReferenceEquals(component, target) || component is WireComponent) continue;
            AdaptiveComponentLayout layout = AdaptiveComponentLayout.Get(component);
            float halfX = (float)layout.HalfWidth + 2;
            float halfY = (float)layout.HalfHeight + 2;
            var body = new RectF((float)component.X - halfX, (float)component.Y - halfY, halfX * 2, halfY * 2);
            if (RectanglesOverlap(body, badge)) return true;
        }

        if (Circuit != null)
        {
            foreach (var wire in components.OfType<WireComponent>())
            {
                var path = SchematicWireRouter.RouteWireComponent(wire, components);
                if (PathIntersectsRectangle(path, badge)) return true;
            }
            foreach (var connection in Circuit.FindChildren<ConnectionNode>())
            {
                var from = Circuit.FindChildById<ComponentNode>(connection.FromComponentId);
                var to = Circuit.FindChildById<ComponentNode>(connection.ToComponentId);
                var fromPin = from?.GetPin(connection.FromPin);
                var toPin = to?.GetPin(connection.ToPin);
                if (from != null && to != null && fromPin != null && toPin != null &&
                    PathIntersectsRectangle(SchematicWireRouter.RouteConnection(from, fromPin, to, toPin, components, connection.RoutingPoints), badge))
                    return true;
            }
        }
        return false;
    }

    private static bool RectanglesOverlap(RectF a, RectF b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    private static bool PathIntersectsRectangle(List<PointF> path, RectF rectangle)
    {
        for (int index = 0; index < path.Count - 1; index++)
        {
            var a = path[index];
            var b = path[index + 1];
            if (MathF.Abs(a.Y - b.Y) < 0.01f)
            {
                if (a.Y >= rectangle.Top && a.Y <= rectangle.Bottom &&
                    Math.Max(Math.Min(a.X, b.X), rectangle.Left) <= Math.Min(Math.Max(a.X, b.X), rectangle.Right)) return true;
            }
            else if (MathF.Abs(a.X - b.X) < 0.01f && a.X >= rectangle.Left && a.X <= rectangle.Right &&
                     Math.Max(Math.Min(a.Y, b.Y), rectangle.Top) <= Math.Min(Math.Max(a.Y, b.Y), rectangle.Bottom)) return true;
        }
        return false;
    }





}
