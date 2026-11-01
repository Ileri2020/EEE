using Microsoft.Maui.Graphics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>
/// Renders the schematic circuit on a MAUI GraphicsView canvas.
/// 
/// Desktop gestures:
///   Left-click              → select component or pin
///   Right-drag              → pan viewport
///   Right-click (no drag)   → open component edit dialog
///   Double-click            → open component edit dialog
///   Pin long-press + drag   → draw wire from that pin to another
///   Middle/scroll wheel     → zoom
///   Pinch (trackpad)        → zoom
///
/// Mobile gestures (controlled by IsMobilePanMode):
///   Pan mode   + one-finger swipe  → pan viewport
///   Pan mode   + long-press comp   + drag → move component
///   Pan mode   + long-press pin    + drag → draw wire
///   Select mode + tap              → select component or pin
///   Select mode + long-press comp  → open edit dialog
///   Select mode + long-press pin   + drag → draw wire
///   Pinch                          → zoom
/// </summary>
public class SchematicViewport : GraphicsView, IDrawable
{
    // ── Bindable: Circuit ────────────────────────────────────────────────────
    public static readonly BindableProperty CircuitProperty =
        BindableProperty.Create(nameof(Circuit), typeof(CircuitNode), typeof(SchematicViewport),
            null, propertyChanged: OnCircuitChanged);

    public CircuitNode? Circuit
    {
        get => (CircuitNode?)GetValue(CircuitProperty);
        set => SetValue(CircuitProperty, value);
    }

    /// <summary>Reflects the ViewModel's IsMobilePanMode; set from WorkspacePage.</summary>
    public bool IsMobilePanMode { get; set; } = true;

    /// <summary>Controls whether electricity movements (animated current dots) are rendered.</summary>
    public bool IsSimulationPlaying { get; set; } = false;
    public float ElectricityAnimPhase { get; set; } = 0.0f;

    // ── Pan / zoom ───────────────────────────────────────────────────────────
    private float _panX, _panY;
    private float _zoom = 1.0f;
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 5.0f;
    private float _lastPanX, _lastPanY;
    private const float GridSize = 20f;

    // ── Events ───────────────────────────────────────────────────────────────
    public event EventHandler<ComponentNode>? ComponentSelected;
    public event EventHandler<(ComponentNode Component, ComponentPin Pin)>? PinSelected;
    public event EventHandler<ComponentNode>? ComponentContextRequested;
    public event EventHandler<(ComponentNode From, ComponentPin FromPin,
                               ComponentNode To,   ComponentPin ToPin)>? WireConnected;

    // ── Interaction state ────────────────────────────────────────────────────
    private ComponentNode? _selectedComponent;
    private (ComponentNode Component, ComponentPin Pin)? _selectedPin;

    // Long-press timer
    private System.Threading.Timer? _longPressTimer;
    private ComponentNode?           _pressedComp;
    private ComponentPin?            _pressedPin;
    private PointF                   _pressWorldPos;

    // Component drag/move
    private bool   _isMovingComponent;
    private PointF _lastMoveScreen;   // screen coords at last move sample

    // Wire draw
    private bool           _isDrawingWire;
    private ComponentNode? _wireSourceComp;
    private ComponentPin?  _wireSourcePin;
    private PointF         _wireCursorWorld;

    public SchematicViewport()
    {
        Drawable        = this;
        BackgroundColor = Color.FromArgb("#0D1117");

        // One-finger pan (also drives component move when _isMovingComponent)
        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += OnPan;
        GestureRecognizers.Add(panGesture);

        // Pinch to zoom
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinch;
        GestureRecognizers.Add(pinchGesture);

        // Left tap → select
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnTapped;
        GestureRecognizers.Add(tapGesture);

        // Double tap → edit dialog
        var doubleTapGesture = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTapGesture.Tapped += OnDoubleTapped;
        GestureRecognizers.Add(doubleTapGesture);

        // Secondary button tap (non-Windows fallback for right-click)
        var rightClickGesture = new TapGestureRecognizer { Buttons = ButtonsMask.Secondary };
        rightClickGesture.Tapped += OnRightClicked;
        GestureRecognizers.Add(rightClickGesture);

        // Pointer events: long-press detection, pointer move for wire preview
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerPressed  += OnPointerPressed;
        pointerGesture.PointerMoved    += OnPointerMoved;
        pointerGesture.PointerReleased += OnPointerReleased;
        GestureRecognizers.Add(pointerGesture);
    }

    // ── Circuit change subscription ───────────────────────────────────────────────
    private static void OnCircuitChanged(BindableObject b, object? oldValue, object? newValue)
    {
        if (b is SchematicViewport vp)
        {
            if (oldValue is CircuitNode old)
            {
                old.OnChildAdded   -= vp.OnCircuitStructureChanged;
                old.OnChildRemoved -= vp.OnCircuitStructureChanged;
                old.OnModified     -= vp.OnCircuitStructureChanged;
            }
            if (newValue is CircuitNode cur)
            {
                cur.OnChildAdded   += vp.OnCircuitStructureChanged;
                cur.OnChildRemoved += vp.OnCircuitStructureChanged;
                cur.OnModified     += vp.OnCircuitStructureChanged;
            }
            vp.Invalidate();
        }
    }

    private void OnCircuitStructureChanged(object? sender, EventArgs e)
        => Dispatcher.Dispatch(Invalidate);

    // ── Windows native right-drag-to-pan + scroll-to-zoom ──────────────────
#if WINDOWS
    private bool                     _isRightPanning;
    private bool                     _isRightCompMove;
    private ComponentNode?           _rightClickedComp;
    private Windows.Foundation.Point _lastNativeRightPos;
    private double                   _rightPanTotalDist;

    public new void Invalidate()
    {
        base.Invalidate();
        try
        {
            if (Handler?.PlatformView is Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl cc)
            {
                cc.Invalidate();
            }
            else if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement ui)
            {
                ui.InvalidateArrange();
            }
        }
        catch { }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement el)
        {
            el.PointerPressed      -= OnNativePtrPressed;
            el.PointerMoved        -= OnNativePtrMoved;
            el.PointerReleased     -= OnNativePtrReleased;
            el.PointerWheelChanged -= OnNativePtrWheel;

            el.PointerPressed      += OnNativePtrPressed;
            el.PointerMoved        += OnNativePtrMoved;
            el.PointerReleased     += OnNativePtrReleased;
            el.PointerWheelChanged += OnNativePtrWheel;
        }
    }

    private void OnNativePtrWheel(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var p     = e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement);
        float dir = p.Properties.MouseWheelDelta > 0 ? 1.1f : (1f / 1.1f);
        _zoom = Math.Clamp(_zoom * dir, MinZoom, MaxZoom);
        Invalidate();
        e.Handled = true;
    }

    private void OnNativePtrPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement);
        if (p.Properties.IsRightButtonPressed)
        {
            var w = ToWorld((float)p.Position.X, (float)p.Position.Y);
            var hitComp = HitTest(w.X, w.Y);
            if (hitComp != null)
            {
                _isRightCompMove    = true;
                _isRightPanning     = false;
                _rightClickedComp   = hitComp;
                _selectedComponent  = hitComp;
                _rightPanTotalDist  = 0;
                _lastNativeRightPos = p.Position;
                (sender as Microsoft.UI.Xaml.UIElement)?.CapturePointer(e.Pointer);
                Invalidate();
                e.Handled = true;
            }
            else
            {
                _isRightPanning     = true;
                _isRightCompMove    = false;
                _rightClickedComp   = null;
                _rightPanTotalDist  = 0;
                _lastNativeRightPos = p.Position;
                (sender as Microsoft.UI.Xaml.UIElement)?.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }
    }

    private void OnNativePtrMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement);
        float dx = (float)(p.Position.X - _lastNativeRightPos.X);
        float dy = (float)(p.Position.Y - _lastNativeRightPos.Y);

        if (_isRightCompMove && _rightClickedComp != null)
        {
            _rightPanTotalDist += Math.Abs(dx) + Math.Abs(dy);
            _rightClickedComp.X += dx / _zoom;
            _rightClickedComp.Y += dy / _zoom;
            _lastNativeRightPos  = p.Position;
            Invalidate();
            e.Handled = true;
        }
        else if (_isRightPanning)
        {
            _rightPanTotalDist  += Math.Abs(dx) + Math.Abs(dy);
            _panX += dx;
            _panY += dy;
            _lastNativeRightPos  = p.Position;
            Invalidate();
            e.Handled = true;
        }

        if (_isDrawingWire)
        {
            _wireCursorWorld = ToWorld((float)p.Position.X, (float)p.Position.Y);
            Invalidate();
        }

        if (_isMovingComponent && _pressedComp != null)
        {
            float sx = (float)p.Position.X;
            float sy = (float)p.Position.Y;
            _pressedComp.X += (sx - _lastMoveScreen.X) / _zoom;
            _pressedComp.Y += (sy - _lastMoveScreen.Y) / _zoom;
            _lastMoveScreen  = new PointF(sx, sy);
            Invalidate();
        }
    }

    private void OnNativePtrReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement);
        if (_isRightCompMove && _rightClickedComp != null)
        {
            (sender as Microsoft.UI.Xaml.UIElement)?.ReleasePointerCapture(e.Pointer);
            if (_rightPanTotalDist < 6)
            {
                var comp = _rightClickedComp;
                _selectedComponent = comp;
                Invalidate();
                ComponentContextRequested?.Invoke(this, comp);
            }
            _isRightCompMove  = false;
            _rightClickedComp = null;
            e.Handled = true;
        }
        else if (_isRightPanning)
        {
            _isRightPanning = false;
            (sender as Microsoft.UI.Xaml.UIElement)?.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        if (_isDrawingWire)
        {
            TryCompleteWire(ToWorld((float)p.Position.X, (float)p.Position.Y));
            e.Handled = true;
        }
        _isMovingComponent = false;
    }
#endif

    // ── Cross-platform gesture handlers ───────────────────────────────────
    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                // Cancel pending long-press unless already moving a component
                if (!_isMovingComponent)
                {
                    _longPressTimer?.Dispose();
                    _longPressTimer = null;
                    _pressedComp    = null;
                }
                _lastPanX = _panX;
                _lastPanY = _panY;
                break;

            case GestureStatus.Running:
                if (_isMovingComponent && _pressedComp != null)
                {
                    // Route pan delta into component position
                    float wx = _lastPanX + (float)e.TotalX;
                    float wy = _lastPanY + (float)e.TotalY;
                    _pressedComp.X += (wx - _lastPanX) / _zoom;
                    _pressedComp.Y += (wy - _lastPanY) / _zoom;
                    _lastPanX = wx;
                    _lastPanY = wy;
                    Invalidate();
                }
                else if (_isDrawingWire)
                {
                    // nothing — wire cursor is updated via PointerMoved
                }
                else if (IsMobilePanMode || DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    _panX = _lastPanX + (float)e.TotalX;
                    _panY = _lastPanY + (float)e.TotalY;
                    Invalidate();
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isMovingComponent = false;
                break;
        }
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Running)
        {
            _zoom = Math.Clamp(_zoom * (float)e.Scale, MinZoom, MaxZoom);
            Invalidate();
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        // Mobile pan-mode taps don't select (pan gesture handles movement)
        if (IsMobilePanMode && DeviceInfo.Platform != DevicePlatform.WinUI) return;

        var pos = e.GetPosition(this);
        if (pos == null || Circuit == null) return;

        var w = ToWorld((float)pos.Value.X, (float)pos.Value.Y);

        // Prefer pin hit
        var pin = HitTestPin(w);
        if (pin != null)
        {
            _selectedPin       = pin;
            _selectedComponent = pin.Value.Component;
            Invalidate();
            PinSelected?.Invoke(this, pin.Value);
            ComponentSelected?.Invoke(this, pin.Value.Component);
            return;
        }
        _selectedPin       = null;
        _selectedComponent = HitTest(w);
        Invalidate();
        if (_selectedComponent != null)
            ComponentSelected?.Invoke(this, _selectedComponent);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null || Circuit == null) return;
        var comp = HitTest(ToWorld((float)pos.Value.X, (float)pos.Value.Y));
        if (comp != null)
        {
            _selectedComponent = comp;
            Invalidate();
            ComponentContextRequested?.Invoke(this, comp);
        }
    }

    private void OnRightClicked(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null || Circuit == null) return;
        var comp = HitTest(ToWorld((float)pos.Value.X, (float)pos.Value.Y));
        if (comp != null)
        {
            _selectedComponent = comp;
            Invalidate();
            ComponentContextRequested?.Invoke(this, comp);
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null || Circuit == null) return;

        var w = ToWorld((float)pos.Value.X, (float)pos.Value.Y);
        _pressWorldPos = w;

        // ── Pin long-press → wire draw ─────────────────────────────────
        var hitPin = HitTestPin(w);
        if (hitPin != null)
        {
            var (pinComp, pin) = hitPin.Value;
            _pressedPin = pin;
            _longPressTimer?.Dispose();
            _longPressTimer = new System.Threading.Timer(_ =>
            {
                Dispatcher.Dispatch(() =>
                {
                    _isDrawingWire   = true;
                    _wireSourceComp  = pinComp;
                    _wireSourcePin   = pin;
                    _wireCursorWorld = new PointF(
                        (float)(pinComp.X + pin.RelativeX),
                        (float)(pinComp.Y + pin.RelativeY));
                    Invalidate();
                });
            }, null, 350, System.Threading.Timeout.Infinite);
            return;
        }

        // ── Component long-press: move (pan mode) or dialog (select mode) ───
        _pressedPin  = null;
        _pressedComp = HitTest(w);
        if (_pressedComp != null)
        {
            var comp = _pressedComp;
            var sx   = (float)pos.Value.X;
            var sy   = (float)pos.Value.Y;
            _lastMoveScreen = new PointF(sx, sy);

            _longPressTimer?.Dispose();
            _longPressTimer = new System.Threading.Timer(_ =>
            {
                if (_pressedComp != comp) return;
                Dispatcher.Dispatch(() =>
                {
                    _selectedComponent = comp;
                    if (IsMobilePanMode)
                    {
                        // Pan mode: long-press → move component
                        _isMovingComponent = true;
                    }
                    else
                    {
                        // Select mode: long-press → open dialog
                        _pressedComp = null;
                        ComponentContextRequested?.Invoke(this, comp);
                    }
                    Invalidate();
                });
            }, null, 480, System.Threading.Timeout.Infinite);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null) return;

        if (_isDrawingWire)
        {
            _wireCursorWorld = ToWorld((float)pos.Value.X, (float)pos.Value.Y);
            Invalidate();
        }
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        _longPressTimer?.Dispose();
        _longPressTimer = null;

        if (_isDrawingWire)
        {
            var pos = e.GetPosition(this);
            TryCompleteWire(pos != null ? ToWorld((float)pos.Value.X, (float)pos.Value.Y) : _wireCursorWorld);
            return;
        }

        _isMovingComponent = false;
        _pressedComp       = null;
        _pressedPin        = null;
    }

    // ── Coordinate helper ─────────────────────────────────────────────────────
    private PointF ToWorld(float sx, float sy) => new((sx - _panX) / _zoom, (sy - _panY) / _zoom);

    // ── Hit test ─────────────────────────────────────────────────────────────
    private (ComponentNode Component, ComponentPin Pin)? HitTestPin(PointF p) => HitTestPin(p.X, p.Y);

    private (ComponentNode Component, ComponentPin Pin)? HitTestPin(float wx, float wy)
    {
        if (Circuit == null) return null;
        const float pinHitRadius = 10.0f;

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

            foreach (var pin in comp.Pins)
            {
                float px = (float)pin.RelativeX;
                float py = (float)pin.RelativeY;
                float distSq = (dx - px) * (dx - px) + (dy - py) * (dy - py);
                if (distSq <= pinHitRadius * pinHitRadius)
                {
                    return (comp, pin);
                }
            }
        }
        return null;
    }

    private ComponentNode? HitTest(PointF p) => HitTest(p.X, p.Y);

    private ComponentNode? HitTest(float wx, float wy)
    {
        if (Circuit == null) return null;
        foreach (var node in Circuit.FindChildren<ComponentNode>())
        {
            float dx = wx - (float)node.X;
            float dy = wy - (float)node.Y;
            if (Math.Abs(dx) <= 26 && Math.Abs(dy) <= 26)
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

    // ── IDrawable ────────────────────────────────────────────────────────────
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
            DrawComponents(canvas);
            DrawWireInProgress(canvas);
        }

        canvas.RestoreState();

        // Draw HUD in screen space (after RestoreState)
        DrawViewportLabel(canvas, dirtyRect);
    }

    private void DrawWireInProgress(ICanvas canvas)
    {
        if (!_isDrawingWire || _wireSourceComp == null || _wireSourcePin == null) return;

        float x1 = (float)(_wireSourceComp.X + _wireSourcePin.RelativeX);
        float y1 = (float)(_wireSourceComp.Y + _wireSourcePin.RelativeY);
        float x2 = _wireCursorWorld.X;
        float y2 = _wireCursorWorld.Y;

        var targetHit = HitTestPin(_wireCursorWorld.X, _wireCursorWorld.Y);
        if (targetHit != null && (targetHit.Value.Component != _wireSourceComp || targetHit.Value.Pin != _wireSourcePin))
        {
            x2 = (float)(targetHit.Value.Component.X + targetHit.Value.Pin.RelativeX);
            y2 = (float)(targetHit.Value.Component.Y + targetHit.Value.Pin.RelativeY);

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

    private void DrawConnections(ICanvas canvas)
    {
        if (Circuit == null) return;
        foreach (var conn in Circuit.FindChildren<ConnectionNode>())
        {
            var fromComp = Circuit.FindChildById<ComponentNode>(conn.FromComponentId);
            var toComp   = Circuit.FindChildById<ComponentNode>(conn.ToComponentId);
            if (fromComp == null || toComp == null) continue;

            var fromPin = fromComp.GetPin(conn.FromPin);
            var toPin   = toComp.GetPin(conn.ToPin);
            if (fromPin == null || toPin == null) continue;

            float x1 = (float)(fromComp.X + fromPin.RelativeX);
            float y1 = (float)(fromComp.Y + fromPin.RelativeY);
            float x2 = (float)(toComp.X + toPin.RelativeX);
            float y2 = (float)(toComp.Y + toPin.RelativeY);

            // Draw wire with elbow routing
            canvas.StrokeColor = Color.FromArgb("#58A6FF");
            canvas.StrokeSize = 1.5f;
            float midX = (x1 + x2) / 2f;
            canvas.DrawLine(x1, y1, midX, y1);
            canvas.DrawLine(midX, y1, midX, y2);
            canvas.DrawLine(midX, y2, x2, y2);

            // Junction dots at endpoints
            canvas.FillColor = Color.FromArgb("#58A6FF");
            canvas.FillCircle(x1, y1, 3);
            canvas.FillCircle(x2, y2, 3);

            // Flowing electricity charges along connected wires when simulation is playing
            if (IsSimulationPlaying)
            {
                DrawElectricityDots(canvas, x1, y1, midX, y1, midX, y2, x2, y2);
            }
        }
    }

    private void DrawElectricityDots(ICanvas canvas, float x1, float y1, float mx1, float my1, float mx2, float my2, float x2, float y2)
    {
        float len1 = MathF.Abs(mx1 - x1);
        float len2 = MathF.Abs(my2 - my1);
        float len3 = MathF.Abs(x2 - mx2);
        float totalLen = len1 + len2 + len3;
        if (totalLen <= 0.1f) return;

        int dotCount = Math.Clamp((int)(totalLen / 28f), 2, 6);
        for (int i = 0; i < dotCount; i++)
        {
            float norm = ((float)i / dotCount + ElectricityAnimPhase) % 1.0f;
            float targetDist = norm * totalLen;

            float dotX, dotY;
            if (targetDist <= len1)
            {
                float t = len1 > 0 ? targetDist / len1 : 0;
                dotX = x1 + (mx1 - x1) * t;
                dotY = y1;
            }
            else if (targetDist <= len1 + len2)
            {
                float t = len2 > 0 ? (targetDist - len1) / len2 : 0;
                dotX = mx1;
                dotY = my1 + (my2 - my1) * t;
            }
            else
            {
                float t = len3 > 0 ? (targetDist - len1 - len2) / len3 : 0;
                dotX = mx2 + (x2 - mx2) * t;
                dotY = y2;
            }

            // Outer electric halo + inner bright charge dot
            canvas.FillColor = Color.FromArgb("#6656D364");
            canvas.FillCircle(dotX, dotY, 4.0f);
            canvas.FillColor = Color.FromArgb("#E3B341");
            canvas.FillCircle(dotX, dotY, 2.2f);
        }
    }

    private void DrawComponents(ICanvas canvas)
    {
        if (Circuit == null) return;
        foreach (var comp in Circuit.FindChildren<ComponentNode>())
        {
            bool selected = comp == _selectedComponent;
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

        // Selection glow
        if (selected)
        {
            canvas.StrokeColor = Color.FromArgb("#79C0FF");
            canvas.StrokeSize = 3f;
            canvas.DrawRoundedRectangle(-26, -26, 52, 52, 6);
        }

        switch (comp.ComponentType)
        {
            case "Resistor":      DrawResistor(canvas); break;
            case "Capacitor":     DrawCapacitor(canvas); break;
            case "Inductor":      DrawInductor(canvas); break;
            case "VoltageSource": DrawVoltageSource(canvas); break;
            case "CurrentSource": DrawCurrentSource(canvas); break;
            case "Ground":        DrawGround(canvas); break;
            case "Diode":         DrawDiode(canvas); break;
            case "LED":           DrawLED(canvas); break;
            case "BJT":           DrawBJT(canvas); break;
            case "MOSFET":        DrawMOSFET(canvas); break;
            case "OpAmp":         DrawOpAmp(canvas); break;
            case "LogicGate":
            case "AND":           DrawAndGate(canvas); break;
            case "OR":            DrawOrGate(canvas); break;
            case "NOT":           DrawNotGate(canvas); break;
            case "NAND":          DrawNandGate(canvas); break;
            case "NOR":           DrawNorGate(canvas); break;
            case "XOR":           DrawXorGate(canvas); break;
            case "XNOR":          DrawXnorGate(canvas); break;
            case "IC":            DrawIC(canvas); break;
            case "Junction":      DrawJunction(canvas); break;
            case "Label":            DrawLabel(canvas, comp.Name); break;
            case "MutualInductance": ComponentSymbolDrawer.DrawMutualInductance(canvas); break;
            default:                 DrawGenericComponent(canvas, comp.ComponentType); break;
        }

        // Label
        canvas.FontColor = Color.FromArgb("#8B949E");
        canvas.FontSize = 9;
        canvas.DrawString(comp.Reference.Length > 0 ? comp.Reference : comp.Name,
            -30, 22, 60, 14, HorizontalAlignment.Center, VerticalAlignment.Top);

        // Live probe HUD (Eye icon probe enabled)
        if (comp.ShowLiveProbe)
        {
            float badgeX = 26;
            float badgeY = -18;
            canvas.FillColor = Color.FromArgb("#161B22");
            canvas.FillRoundedRectangle(badgeX, badgeY, 80, 28, 4);
            canvas.StrokeColor = Color.FromArgb("#56D364");
            canvas.StrokeSize = 1.2f;
            canvas.DrawRoundedRectangle(badgeX, badgeY, 80, 28, 4);

            canvas.FontColor = Color.FromArgb("#56D364");
            canvas.FontSize = 8.5f;
            canvas.DrawString($"👁 {comp.LiveVoltage:F2} V", badgeX + 4, badgeY + 2, 72, 12, HorizontalAlignment.Left, VerticalAlignment.Center);
            canvas.FontColor = Color.FromArgb("#79C0FF");
            canvas.DrawString($"  {comp.LiveCurrent * 1000:F1} mA", badgeX + 4, badgeY + 14, 72, 12, HorizontalAlignment.Left, VerticalAlignment.Center);
        }

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

        // Draw small circle to nodes/pins on all components in viewport
        foreach (var pin in comp.Pins)
        {
            float px = (float)pin.RelativeX;
            float py = (float)pin.RelativeY;
            bool isPinSelected = (_selectedPin != null &&
                                  _selectedPin.Value.Component == comp &&
                                  _selectedPin.Value.Pin == pin);

            // Small circle terminal for connection/selection
            canvas.FillColor = isPinSelected ? Color.FromArgb("#58A6FF") : Color.FromArgb("#0D1117");
            canvas.FillCircle(px, py, 3.5f);

            canvas.StrokeColor = isPinSelected ? Color.FromArgb("#79C0FF") : Color.FromArgb("#58A6FF");
            canvas.StrokeSize = isPinSelected ? 2.0f : 1.2f;
            canvas.DrawCircle(px, py, 3.5f);

            // Outer selection halo if selected
            if (isPinSelected)
            {
                canvas.StrokeColor = Color.FromArgb("#58A6FF");
                canvas.StrokeSize = 1.0f;
                canvas.DrawCircle(px, py, 6.0f);
            }
        }

        canvas.RestoreState();
    }

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

    private static void DrawJunction(ICanvas canvas)
    {
        canvas.FillColor = Color.FromArgb("#58A6FF");
        canvas.FillCircle(0, 0, 4);
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
