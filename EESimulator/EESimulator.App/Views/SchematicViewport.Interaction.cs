using Microsoft.Maui.Graphics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

public partial class SchematicViewport
{
    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null || Circuit == null) return;

        var w = ToWorld((float)pos.Value.X, (float)pos.Value.Y);

        // Check rotate handle first
        if (_rotateHandleVisible)
        {
            float rdx = w.X - _rotateHandleWorld.X;
            float rdy = w.Y - _rotateHandleWorld.Y;
            if (rdx * rdx + rdy * rdy <= RotateHandleRadius * RotateHandleRadius * 2.5f)
            {
                if (_selectedComponent != null)
                {
                    ComponentRotateRequested?.Invoke(this, _selectedComponent);
                    Invalidate();
                    return;
                }
            }
        }

        // Check stretch handles if a component is selected
        if (_selectedComponent != null)
        {
            int stretchHandle = HitTestStretchHandle(w);
            if (stretchHandle >= 0)
            {
                _isStretchingComponent = true;
                _stretchHandleIndex = stretchHandle;
                _stretchStartWorld = w;
                _initialScaleX = _selectedComponent.ScaleX > 0 ? _selectedComponent.ScaleX : 1.0;
                _initialScaleY = _selectedComponent.ScaleY > 0 ? _selectedComponent.ScaleY : 1.0;
                _stretchScaleWidth = GetSelectionHalfWidth(_selectedComponent) / (float)_initialScaleX;
                _stretchScaleHeight = GetSelectionHalfHeight(_selectedComponent) / (float)_initialScaleY;
                Invalidate();
                return;
            }
        }

    }



    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null) return;

        if (_isStretchingComponent && _selectedComponent != null)
        {
            var w = ToWorld((float)pos.Value.X, (float)pos.Value.Y);
            float ddx = w.X - _stretchStartWorld.X;
            float ddy = w.Y - _stretchStartWorld.Y;
            float rad = (float)(-_selectedComponent.Rotation * Math.PI / 180.0);
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);
            float ldx = (ddx * cos - ddy * sin) / _stretchScaleWidth;
            float ldy = (ddx * sin + ddy * cos) / _stretchScaleHeight;

            double newSx = _initialScaleX;
            double newSy = _initialScaleY;

            if (_stretchHandleIndex is 2 or 4 or 7) newSx = Math.Clamp(_initialScaleX + ldx, 0.3, 5.0);
            else if (_stretchHandleIndex is 0 or 3 or 5) newSx = Math.Clamp(_initialScaleX - ldx, 0.3, 5.0);

            if (_stretchHandleIndex is 5 or 6 or 7) newSy = Math.Clamp(_initialScaleY + ldy, 0.3, 5.0);
            else if (_stretchHandleIndex is 0 or 1 or 2) newSy = Math.Clamp(_initialScaleY - ldy, 0.3, 5.0);

            _selectedComponent.ScaleX = newSx;
            _selectedComponent.ScaleY = newSy;
            UpdateRotateHandle();
            Invalidate();
            return;
        }

        if (_isDrawingWire)
        {
            _wireCursorWorld = ToWorld((float)pos.Value.X, (float)pos.Value.Y);
            Invalidate();
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        _isStretchingComponent = false;
        _stretchHandleIndex = -1;
    }

    private void FinishComponentDrag()
    {
        if (!_isMovingComponent || _pressedComp == null) return;

        var component = _pressedComp;
        _isMovingComponent = false;
        if (_componentDragMoved)
        {
            InvalidateWireGeometryCache();
            ComponentMoveCommitted?.Invoke(this, (
                component,
                _componentDragStartX,
                _componentDragStartY,
                component.X,
                component.Y));
        }
        else
        {
        }
        _componentDragMoved = false;
        _pressedComp = null;
    }

    // ── Coordinate helper ─────────────────────────────────────────────────────
    private PointF ToWorld(float sx, float sy) => new((sx - _panX) / _zoom, (sy - _panY) / _zoom);

    private void ZoomAt(float screenX, float screenY, float factor)
    {
        var worldPoint = ToWorld(screenX, screenY);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        _panX = screenX - worldPoint.X * _zoom;
        _panY = screenY - worldPoint.Y * _zoom;
        Invalidate();
    }

    public void ZoomBy(float factor)
        => ZoomAt((float)Width / 2f, (float)Height / 2f, factor);

    public (double X, double Y) GetVisibleTopRightWorldPosition()
    {
        if (Width <= 0 || Height <= 0)
            return (0, 0);

        const float screenMargin = 80f;
        float screenX = Math.Clamp((float)Width - screenMargin, 0f, (float)Width);
        float screenY = Math.Clamp(screenMargin, 0f, (float)Height);
        PointF world = ToWorld(screenX, screenY);
        return (world.X, world.Y);
    }

    public void FitToScreen()
    {
        if (Circuit == null) return;

        var components = Circuit.FindChildren<ComponentNode>()
            .Where(component => component is not WireComponent)
            .ToList();
        if (components.Count == 0 || Width <= 0 || Height <= 0) return;

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        foreach (var component in components)
        {
            float halfWidth = GetSelectionHalfWidth(component);
            float halfHeight = GetSelectionHalfHeight(component);
            float radians = (float)(component.Rotation * Math.PI / 180.0);
            float extentX = MathF.Abs(MathF.Cos(radians)) * halfWidth +
                MathF.Abs(MathF.Sin(radians)) * halfHeight;
            float extentY = MathF.Abs(MathF.Sin(radians)) * halfWidth +
                MathF.Abs(MathF.Cos(radians)) * halfHeight;

            minX = MathF.Min(minX, (float)component.X - extentX);
            maxX = MathF.Max(maxX, (float)component.X + extentX);
            minY = MathF.Min(minY, (float)component.Y - extentY);
            maxY = MathF.Max(maxY, (float)component.Y + extentY);
        }

        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;
        float contentWidth = MathF.Max(maxX - minX, 1f);
        float contentHeight = MathF.Max(maxY - minY, 1f);
        const float padding = 32f;
        float availableWidth = MathF.Max((float)Width - 2f * padding, 1f);
        float availableHeight = MathF.Max((float)Height - 2f * padding, 1f);

        RecenterToCircuit();
        _zoom = Math.Clamp(
            MathF.Min(availableWidth / contentWidth, availableHeight / contentHeight),
            MinZoom,
            MaxZoom);
        _panX = (float)Width / 2f - centerX * _zoom;
        _panY = (float)Height / 2f - centerY * _zoom;
        Invalidate();
    }

    /// <summary>Centers the viewport on the mean position of placed circuit components.</summary>
    public void RecenterToCircuit()
    {
        if (Circuit == null) return;
        var components = Circuit.FindChildren<ComponentNode>()
            .Where(component => component is not WireComponent)
            .ToList();
        if (components.Count == 0) return;

        float centerX = (float)components.Average(component => component.X);
        float centerY = (float)components.Average(component => component.Y);
        _panX = (float)Width / 2f - centerX * _zoom;
        _panY = (float)Height / 2f - centerY * _zoom;
        Invalidate();
    }

    // ── Hit test ─────────────────────────────────────────────────────────────
    private (ComponentNode Component, ComponentPin Pin)? HitTestPin(PointF p) => HitTestPin(p.X, p.Y);

    private (ComponentNode Component, ComponentPin Pin)? HitTestPin(float wx, float wy)
    {
        if (Circuit == null) return null;
        float pinHitRadius = DeviceInfo.Idiom == DeviceIdiom.Phone ? 24.0f : 16.0f;
        float nearestDistanceSq = pinHitRadius * pinHitRadius;
        (ComponentNode Component, ComponentPin Pin)? nearestPin = null;

        foreach (var comp in Circuit.FindChildren<ComponentNode>())
        {
            float dx = wx - (float)comp.X;
            float dy = wy - (float)comp.Y;

            if (comp.Rotation != 0)
            {
                float rad = -(float)(comp.Rotation * Math.PI / 180.0);
                float rx = dx * MathF.Cos(rad) - dy * MathF.Sin(rad);
                float ry = dx * MathF.Sin(rad) + dy * MathF.Cos(rad);
                dx = rx;
                dy = ry;
            }

            AdaptiveComponentLayout layout = AdaptiveComponentLayout.Get(comp);
            foreach (var pin in comp.Pins)
            {
                ComponentPinPosition position = layout.GetPinPosition(pin);
                float px = (float)position.X;
                float py = (float)position.Y;
                float distSq = (dx - px) * (dx - px) + (dy - py) * (dy - py);
                if (distSq <= nearestDistanceSq)
                {
                    nearestDistanceSq = distSq;
                    nearestPin = (comp, pin);
                }
            }
        }
        return nearestPin;
    }

    private ComponentNode? HitTest(PointF p) => HitTest(p.X, p.Y);

    private ConnectionNode? HitTestConnection(PointF point)
    {
        if (Circuit == null) return null;
        var components = Circuit.FindChildren<ComponentNode>().ToList();
        foreach (var connection in Circuit.FindChildren<ConnectionNode>().Reverse())
        {
            var from = Circuit.FindChildById<ComponentNode>(connection.FromComponentId);
            var to = Circuit.FindChildById<ComponentNode>(connection.ToComponentId);
            var fromPin = from?.GetPin(connection.FromPin);
            var toPin = to?.GetPin(connection.ToPin);
            if (from == null || to == null || fromPin == null || toPin == null) continue;

            var path = SchematicWireRouter.RouteConnection(from, fromPin, to, toPin, components, connection.RoutingPoints);
            for (int index = 0; index < path.Count - 1; index++)
                if (DistanceToSegment(point, path[index], path[index + 1]) <= 9f)
                    return connection;
        }
        return null;
    }

    private ComponentNode? HitTest(float wx, float wy)
    {
        if (Circuit == null) return null;
        var point = new PointF(wx, wy);
        var components = Circuit.FindChildren<ComponentNode>().ToList();
        foreach (var wire in components.OfType<WireComponent>())
        {
            var path = SchematicWireRouter.RouteWireComponent(wire, components);
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (DistanceToSegment(point, path[i], path[i + 1]) <= 9f)
                    return wire;
            }
        }

        foreach (var node in Circuit.FindChildren<ComponentNode>())
        {
            if (node is WireComponent) continue;
            float dx = wx - (float)node.X;
            float dy = wy - (float)node.Y;
            float radians = -(float)(node.Rotation * Math.PI / 180.0);
            float localX = dx * MathF.Cos(radians) - dy * MathF.Sin(radians);
            float localY = dx * MathF.Sin(radians) + dy * MathF.Cos(radians);
            AdaptiveComponentLayout layout = AdaptiveComponentLayout.Get(node);
            if (Math.Abs(localX) <= layout.HalfWidth && Math.Abs(localY) <= layout.HalfHeight)
                return node;
        }
        return null;
    }

    // ── Wire connection helpers ───────────────────────────────────────────────
    private void TryCompleteWire(PointF endWorld)
    {
        if (!_isDrawingWire || _wireSourceComp == null || _wireSourcePin == null)
        {
            CancelWire();
            return;
        }

        var sourceComp = _wireSourceComp;
        var sourcePin  = _wireSourcePin;
        CancelWire();

        if (Circuit == null) return;

        var targetHit = HitTestPin(endWorld.X, endWorld.Y);
        if (targetHit != null && (targetHit.Value.Component != sourceComp || targetHit.Value.Pin != sourcePin))
        {
            // Connect to hit pin
            WireConnected?.Invoke(this, (sourceComp, sourcePin, targetHit.Value.Component, targetHit.Value.Pin));
        }
        else
        {
            // Ends on workspace: create a JunctionComponent node at that coordinate and connect
            var junc = new JunctionComponent
            {
                Name = $"J_{Guid.NewGuid().ToString("N")[..4]}",
                Reference = $"J_{Guid.NewGuid().ToString("N")[..4]}",
                X = endWorld.X,
                Y = endWorld.Y
            };
            Circuit.AddChild(junc);
            var juncPin = junc.Pins.FirstOrDefault() ?? new ComponentPin("1", "1", 0, 0);
            WireConnected?.Invoke(this, (sourceComp, sourcePin, junc, juncPin));
        }
        Invalidate();
    }

    private void CancelWire()
    {
        _isDrawingWire   = false;
        _wireSourceComp  = null;
        _wireSourcePin   = null;
        Invalidate();
    }

    /// <summary>
    /// Slices or turns touched wire component or connection into a JunctionComponent node.
    /// Triggered when the main sidebar cut wire button is active.
    /// </summary>
    private bool TryCutWireToNode(PointF w)
    {
        if (Circuit == null) return false;
        const float hitDist = 12.0f;

        // 1. Check ConnectionNodes (elbow connection wires)
        foreach (var conn in Circuit.FindChildren<ConnectionNode>().ToList())
        {
            var fromComp = Circuit.FindChildById<ComponentNode>(conn.FromComponentId);
            var toComp   = Circuit.FindChildById<ComponentNode>(conn.ToComponentId);
            if (fromComp == null || toComp == null) continue;

            var fromPin = fromComp.GetPin(conn.FromPin);
            var toPin   = toComp.GetPin(conn.ToPin);
            if (fromPin == null || toPin == null) continue;

            var fromPoint = SchematicWireRouter.GetPinWorldPosition(fromComp, fromPin);
            var toPoint = SchematicWireRouter.GetPinWorldPosition(toComp, toPin);
            float x1 = fromPoint.X;
            float y1 = fromPoint.Y;
            float x2 = toPoint.X;
            float y2 = toPoint.Y;
            float midX = (x1 + x2) / 2f;

            if (DistanceToSegment(w, new PointF(x1, y1), new PointF(midX, y1)) <= hitDist ||
                DistanceToSegment(w, new PointF(midX, y1), new PointF(midX, y2)) <= hitDist ||
                DistanceToSegment(w, new PointF(midX, y2), new PointF(x2, y2)) <= hitDist)
            {
                // Create a Junction node at touched position (snapped to 10px grid)
                float jx = MathF.Round(w.X / 10f) * 10f;
                float jy = MathF.Round(w.Y / 10f) * 10f;
                var junc = new JunctionComponent
                {
                    Name = $"J_{Guid.NewGuid().ToString("N")[..4]}",
                    Reference = $"J_{Guid.NewGuid().ToString("N")[..4]}",
                    X = jx,
                    Y = jy
                };
                Circuit.AddChild(junc);
                var juncPin = junc.Pins.FirstOrDefault() ?? new ComponentPin("1", "1", 0, 0);

                // Splice connection: remove old connection and link both sides through the new junction node
                Circuit.RemoveChild(conn);
                Circuit.AddChild(new ConnectionNode(conn.FromComponentId, conn.FromPin, junc.Id, juncPin.Id));
                Circuit.AddChild(new ConnectionNode(junc.Id, juncPin.Id, conn.ToComponentId, conn.ToPin));

                _selectedComponent = junc;
                UpdateRotateHandle();
                ComponentSelected?.Invoke(this, junc);
                WireCutIntoNode?.Invoke(this, junc);
                return true;
            }
        }

        // 2. Check standalone WireComponents
        foreach (var comp in Circuit.FindChildren<ComponentNode>().Where(c => c.ComponentType == "Wire").ToList())
        {
            float dx = w.X - (float)comp.X;
            float dy = w.Y - (float)comp.Y;
            if (MathF.Abs(dx) <= 24 && MathF.Abs(dy) <= 24)
            {
                var junc = new JunctionComponent
                {
                    Name = comp.Name,
                    Reference = comp.Reference,
                    X = comp.X,
                    Y = comp.Y
                };
                Circuit.RemoveChild(comp);
                Circuit.AddChild(junc);
                _selectedComponent = junc;
                UpdateRotateHandle();
                ComponentSelected?.Invoke(this, junc);
                WireCutIntoNode?.Invoke(this, junc);
                return true;
            }
        }

        return false;
    }

    private static float DistanceToSegment(PointF p, PointF a, PointF b)
    {
        float l2 = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
        if (l2 <= 0.0001f)
            return MathF.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        float t = Math.Clamp(((p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y)) / l2, 0f, 1f);
        float projX = a.X + t * (b.X - a.X);
        float projY = a.Y + t * (b.Y - a.Y);
        return MathF.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
    }

    private int GetPinConnectionCount(ComponentNode comp, ComponentPin pin)
    {
        if (Circuit == null) return 0;
        int count = 0;
        foreach (var conn in Circuit.FindChildren<ConnectionNode>())
        {
            if ((conn.FromComponentId == comp.Id && (conn.FromPin == pin.Id || conn.FromPin == pin.Name)) ||
                (conn.ToComponentId == comp.Id && (conn.ToPin == pin.Id || conn.ToPin == pin.Name)))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Returns a distinct color based on the number of wires connected to a component pin/node (0 through 10+).
    /// </summary>
    public static Color GetNodeConnectionColor(int count)
    {
        return count switch
        {
            0 => Color.FromArgb("#8B949E"), // 0: Slate Gray (no connection / open)
            1 => Color.FromArgb("#58A6FF"), // 1: Electric Blue
            2 => Color.FromArgb("#56D364"), // 2: Bright Emerald Green
            3 => Color.FromArgb("#E3B341"), // 3: Golden Amber
            4 => Color.FromArgb("#D2A8FF"), // 4: Royal Lavender Purple
            5 => Color.FromArgb("#FFA657"), // 5: Vivid Tangerine Orange
            6 => Color.FromArgb("#FF7B72"), // 6: Bright Coral Salmon
            7 => Color.FromArgb("#39D353"), // 7: Neon Lime
            8 => Color.FromArgb("#79C0FF"), // 8: Sky Cyan
            9 => Color.FromArgb("#DB61A2"), // 9: Vibrant Magenta Pink
            _ => Color.FromArgb("#F43F5E")  // 10+: Ruby Crimson
        };
    }


    // ── IDrawable ────────────────────────────────────────────────────────────

}
