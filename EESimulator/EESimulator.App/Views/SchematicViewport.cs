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

    public static readonly BindableProperty LiveMeasurementsVisibleProperty =
        BindableProperty.Create(nameof(LiveMeasurementsVisible), typeof(bool), typeof(SchematicViewport), false,
            propertyChanged: (b, _, _) => ((SchematicViewport)b).Invalidate());

    public bool LiveMeasurementsVisible
    {
        get => (bool)GetValue(LiveMeasurementsVisibleProperty);
        set => SetValue(LiveMeasurementsVisibleProperty, value);
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
    private float _pinchStartZoom = 1.0f;
    private const float GridSize = 20f;

    // ── Events ───────────────────────────────────────────────────────────────
    public event EventHandler<ComponentNode>? ComponentSelected;
    public event EventHandler<(ComponentNode Component, ComponentPin Pin)>? PinSelected;
    public event EventHandler<ComponentNode>? ComponentContextRequested;
    public event EventHandler<ComponentNode>? ComponentLongPressRequested;
    public event EventHandler<(ComponentNode Component, double OldX, double OldY, double NewX, double NewY)>? ComponentMoveCommitted;
    public event EventHandler<ComponentNode>? ComponentRotateRequested;
    public event EventHandler<(ComponentNode From, ComponentPin FromPin,
                               ComponentNode To,   ComponentPin ToPin)>? WireConnected;
    public event EventHandler<JunctionComponent>? WireCutIntoNode;

    /// <summary>Active tool selected in main sidebar (e.g. "Select", "Wire", "Cut", "Pan").</summary>
    public string ActiveTool { get; set; } = "Select";

    // Interaction state ────────────────────────────────────────────────────
    private ComponentNode? _selectedComponent;
    private (ComponentNode Component, ComponentPin Pin)? _selectedPin;

    // Rotate handle: rendered at right edge of selection box
    private const float RotateHandleOffsetX = 36f; // world units from component centre
    private const float RotateHandleRadius   = 7f;
    private bool _rotateHandleVisible;
    private PointF _rotateHandleWorld; // world position of rotate handle centre

    // Stretch handles
    private bool   _isStretchingComponent;
    private int    _stretchHandleIndex = -1;
    private PointF _stretchStartWorld;
    private double _initialScaleX = 1.0;
    private double _initialScaleY = 1.0;

    // Long-press timer
    private System.Threading.Timer? _longPressTimer;
    private ComponentNode?           _pressedComp;
    private ComponentPin?            _pressedPin;
    private PointF                   _pressWorldPos;

    // Component drag/move
    private bool   _isMovingComponent;
    private PointF _lastMoveScreen;   // screen coords at last move sample
    private float _componentDragStartX, _componentDragStartY;
    private float _componentDragStartTotalX, _componentDragStartTotalY;
    private float _lastPanTotalX, _lastPanTotalY;
    private bool _componentDragMoved;

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

    // ── Rotate handle helpers ─────────────────────────────────────────────────
    /// <summary>
    /// Recomputes the world-space position of the rotate handle based on the
    /// currently selected component's position and rotation.
    /// The handle is drawn at (half+14, 0) in component-local space where half=26f.
    /// </summary>
    private void UpdateRotateHandle()
    {
        if (_selectedComponent == null)
        {
            _rotateHandleVisible = false;
            return;
        }

        float sx = (float)(_selectedComponent.ScaleX > 0 ? _selectedComponent.ScaleX : 1.0);
        float localX = (26f * sx) + 14f;
        const float localY = 0f;

        // Rotate the local offset by the component's rotation angle
        float rad = (float)(_selectedComponent.Rotation * Math.PI / 180.0);
        float cos = (float)Math.Cos(rad);
        float sin = (float)Math.Sin(rad);
        float worldX = (float)_selectedComponent.X + localX * cos - localY * sin;
        float worldY = (float)_selectedComponent.Y + localX * sin + localY * cos;

        _rotateHandleWorld   = new PointF(worldX, worldY);
        _rotateHandleVisible = true;
    }

    private int HitTestStretchHandle(PointF worldPos)
    {
        if (_selectedComponent == null) return -1;

        float dx = worldPos.X - (float)_selectedComponent.X;
        float dy = worldPos.Y - (float)_selectedComponent.Y;
        float rad = (float)(-_selectedComponent.Rotation * Math.PI / 180.0);
        float cos = (float)Math.Cos(rad);
        float sin = (float)Math.Sin(rad);
        float lx = dx * cos - dy * sin;
        float ly = dx * sin + dy * cos;

        float sx = (float)(_selectedComponent.ScaleX > 0 ? _selectedComponent.ScaleX : 1.0);
        float sy = (float)(_selectedComponent.ScaleY > 0 ? _selectedComponent.ScaleY : 1.0);
        float halfX = 26f * sx;
        float halfY = 26f * sy;

        float[] hx = { -halfX, 0,     halfX,  -halfX, halfX,  -halfX, 0,     halfX };
        float[] hy = { -halfY, -halfY, -halfY, 0,     0,      halfY,  halfY, halfY };

        const float hitRadiusSq = 12f * 12f;
        for (int i = 0; i < 8; i++)
        {
            float ddx = lx - hx[i];
            float ddy = ly - hy[i];
            if (ddx * ddx + ddy * ddy <= hitRadiusSq)
                return i;
        }
        return -1;
    }

    private void ClearRotateHandle()
    {
        _rotateHandleVisible = false;
        _selectedComponent   = null;
    }



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
                UpdateRotateHandle();
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

        if (_isStretchingComponent && _selectedComponent != null)
        {
            var w = ToWorld((float)p.Position.X, (float)p.Position.Y);
            float ddx = w.X - _stretchStartWorld.X;
            float ddy = w.Y - _stretchStartWorld.Y;
            float rad = (float)(-_selectedComponent.Rotation * Math.PI / 180.0);
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);
            float ldx = (ddx * cos - ddy * sin) / 26f;
            float ldy = (ddx * sin + ddy * cos) / 26f;

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
            e.Handled = true;
            return;
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
            _componentDragMoved = true;
            Invalidate();
        }
    }

    private void OnNativePtrReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement);
        _isStretchingComponent = false;
        _stretchHandleIndex = -1;
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
        FinishComponentDrag();
    }
#endif

    // ── Cross-platform gesture handlers ───────────────────────────────────
    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                // Cancel pending long-press unless already moving a component
                if (!_isMovingComponent && _pressedComp == null)
                {
                    _longPressTimer?.Dispose();
                    _longPressTimer = null;
                }
                _lastPanX = _panX;
                _lastPanY = _panY;
                _lastPanTotalX = 0;
                _lastPanTotalY = 0;
                if (_pressedComp != null)
                {
                    _componentDragStartX = (float)_pressedComp.X;
                    _componentDragStartY = (float)_pressedComp.Y;
                    _componentDragStartTotalX = 0;
                    _componentDragStartTotalY = 0;
                    _componentDragMoved = false;
                }
                break;

            case GestureStatus.Running:
                if (_isMovingComponent && _pressedComp != null)
                {
                    _pressedComp.X = _componentDragStartX + ((float)e.TotalX - _componentDragStartTotalX) / _zoom;
                    _pressedComp.Y = _componentDragStartY + ((float)e.TotalY - _componentDragStartTotalY) / _zoom;
                    _componentDragMoved = true;
                    Invalidate();
                }
                else if (_isDrawingWire)
                {
                    // nothing — wire cursor is updated via PointerMoved
                }
                else if (IsMobilePanMode || DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    _panX = _lastPanX + (float)e.TotalX;
                    _panY = _lastPanY + (float)e.TotalY;
                    Invalidate();
                }
                _lastPanTotalX = (float)e.TotalX;
                _lastPanTotalY = (float)e.TotalY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                FinishComponentDrag();
                break;
        }
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _pinchStartZoom = _zoom;
            return;
        }

        if (e.Status == GestureStatus.Running && e.Scale > 0.05 && e.Scale < 20.0)
        {
            float originX = (float)e.ScaleOrigin.X * (float)Width;
            float originY = (float)e.ScaleOrigin.Y * (float)Height;
            var worldAtOrigin = ToWorld(originX, originY);
            _zoom = Math.Clamp(_pinchStartZoom * (float)e.Scale, MinZoom, MaxZoom);
            _panX = originX - worldAtOrigin.X * _zoom;
            _panY = originY - worldAtOrigin.Y * _zoom;
            Invalidate();
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null || Circuit == null) return;

        var w = ToWorld((float)pos.Value.X, (float)pos.Value.Y);

        if (ActiveTool.Equals("ZoomIn", StringComparison.OrdinalIgnoreCase))
        {
            ZoomAt((float)pos.Value.X, (float)pos.Value.Y, 1.25f);
            return;
        }
        if (ActiveTool.Equals("ZoomOut", StringComparison.OrdinalIgnoreCase))
        {
            ZoomAt((float)pos.Value.X, (float)pos.Value.Y, 1f / 1.25f);
            return;
        }
        if (ActiveTool.Equals("FitAll", StringComparison.OrdinalIgnoreCase))
        {
            RecenterToCircuit();
            return;
        }

        // When cut wire tool is active in main sidebar, touching anywhere on a wire turns it into a node
        if (ActiveTool.Equals("Cut", StringComparison.OrdinalIgnoreCase))
        {
            if (TryCutWireToNode(w))
            {
                Invalidate();
                return;
            }
        }

        // If rotate handle is visible and tapped, rotate immediately
        if (_rotateHandleVisible)
        {
            float rdx = w.X - _rotateHandleWorld.X;
            float rdy = w.Y - _rotateHandleWorld.Y;
            if (rdx * rdx + rdy * rdy <= RotateHandleRadius * RotateHandleRadius * 4)
            {
                if (_selectedComponent != null)
                {
                    ComponentRotateRequested?.Invoke(this, _selectedComponent);
                    Invalidate();
                    return;
                }
            }
        }

        // Prefer pin hit
        var pin = HitTestPin(w);
        if (pin != null)
        {
            _selectedPin       = pin;
            _selectedComponent = pin.Value.Component;
            UpdateRotateHandle();
            Invalidate();
            PinSelected?.Invoke(this, pin.Value);
            ComponentSelected?.Invoke(this, pin.Value.Component);
            return;
        }
        _selectedPin       = null;
        _selectedComponent = HitTest(w);
        UpdateRotateHandle();
        if (_selectedComponent == null)
        {
            Invalidate();
            return;
        }
        if (_selectedComponent is EESimulator.Core.Components.Electronics.LogicInputComponent lin)
        {
            lin.Toggle();
        }
        else if (_selectedComponent is EESimulator.Core.Components.Electronics.ButtonComponent button)
        {
            button.Press();
            Dispatcher?.DispatchDelayed(TimeSpan.FromMilliseconds(120), button.Release);
        }
        else if (_selectedComponent is EESimulator.Core.Components.Electronics.DipSwitchComponent dip)
        {
            dip.Value = (dip.Value ^ 1) & ((1L << dip.PoleCount) - 1);
        }
        else if (_selectedComponent is EESimulator.Core.Components.Electronics.PinComponent pinComp)
        {
            pinComp.Toggle();
        }
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
        else
        {
            ZoomAt((float)pos.Value.X, (float)pos.Value.Y, 1.5f);
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

        // Cut tool handling on pointer press
        if (ActiveTool.Equals("Cut", StringComparison.OrdinalIgnoreCase))
        {
            if (TryCutWireToNode(w))
            {
                Invalidate();
                return;
            }
        }

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
                Invalidate();
                return;
            }
        }

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
                    _wireCursorWorld = SchematicWireRouter.GetPinWorldPosition(pinComp, pin);
                    Invalidate();
                });
            }, null, 350, System.Threading.Timeout.Infinite);
            return;
        }

        // ── Component long-press: hold, then drag to move; release opens component actions ─
        _pressedPin  = null;
        _pressedComp = HitTest(w);
        if (_pressedComp != null)
        {
            // Immediate select on component touch (both pan and select modes)
            _selectedComponent = _pressedComp;
            UpdateRotateHandle();
            ComponentSelected?.Invoke(this, _pressedComp);
            Invalidate();

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
                    UpdateRotateHandle();
                    if (DeviceInfo.Platform == DevicePlatform.WinUI && !IsMobilePanMode)
                    {
                        // Desktop select mode: long-press → open dialog
                        _pressedComp = null;
                        ComponentContextRequested?.Invoke(this, comp);
                    }
                    else
                    {
                        // Touch and desktop pan mode: long-press → drag component.
                        _isMovingComponent = true;
                        _componentDragStartX = (float)comp.X;
                        _componentDragStartY = (float)comp.Y;
                        _componentDragStartTotalX = _lastPanTotalX;
                        _componentDragStartTotalY = _lastPanTotalY;
                        _componentDragMoved = false;
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

        if (_isStretchingComponent && _selectedComponent != null)
        {
            var w = ToWorld((float)pos.Value.X, (float)pos.Value.Y);
            float ddx = w.X - _stretchStartWorld.X;
            float ddy = w.Y - _stretchStartWorld.Y;
            float rad = (float)(-_selectedComponent.Rotation * Math.PI / 180.0);
            float cos = (float)Math.Cos(rad);
            float sin = (float)Math.Sin(rad);
            float ldx = (ddx * cos - ddy * sin) / 26f;
            float ldy = (ddx * sin + ddy * cos) / 26f;

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
        _longPressTimer?.Dispose();
        _longPressTimer = null;
        _isStretchingComponent = false;
        _stretchHandleIndex = -1;

        if (_isDrawingWire)
        {
            var pos = e.GetPosition(this);
            TryCompleteWire(pos != null ? ToWorld((float)pos.Value.X, (float)pos.Value.Y) : _wireCursorWorld);
            return;
        }

        FinishComponentDrag();
        _pressedComp       = null;
        _pressedPin        = null;
    }

    private void FinishComponentDrag()
    {
        if (!_isMovingComponent || _pressedComp == null) return;

        var component = _pressedComp;
        _isMovingComponent = false;
        if (_componentDragMoved)
        {
            ComponentMoveCommitted?.Invoke(this, (
                component,
                _componentDragStartX,
                _componentDragStartY,
                component.X,
                component.Y));
        }
        else
        {
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
                ComponentLongPressRequested?.Invoke(this, component);
            else
                ComponentContextRequested?.Invoke(this, component);
        }
        _componentDragMoved = false;
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

            dx /= (float)(comp.ScaleX > 0 ? comp.ScaleX : 1.0);
            dy /= (float)(comp.ScaleY > 0 ? comp.ScaleY : 1.0);

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

    private void DrawConnections(ICanvas canvas)
    {
        if (Circuit == null) return;
        var components = Circuit.FindChildren<ComponentNode>().ToList();
        var routedWires = new List<(List<PointF> Path, ComponentNode? Owner, int ElectronDirection)>();

        foreach (var wire in components.OfType<WireComponent>())
        {
            var path = SchematicWireRouter.RouteWireComponent(wire, components);
            routedWires.Add((path, wire, wire.LiveCurrent > 0 ? -1 : wire.LiveCurrent < 0 ? 1 : 0));
        }

        foreach (var conn in Circuit.FindChildren<ConnectionNode>())
        {
            var fromComp = Circuit.FindChildById<ComponentNode>(conn.FromComponentId);
            var toComp = Circuit.FindChildById<ComponentNode>(conn.ToComponentId);
            if (fromComp == null || toComp == null) continue;
            var fromPin = fromComp.GetPin(conn.FromPin);
            var toPin = toComp.GetPin(conn.ToPin);
            if (fromPin == null || toPin == null) continue;

            var path = SchematicWireRouter.RouteConnection(fromComp, fromPin, toComp, toPin, components);
            routedWires.Add((path, null, GetElectronDirection(fromComp, fromPin, toComp, toPin)));
        }

        for (int index = 0; index < routedWires.Count; index++)
        {
            var wire = routedWires[index];
            var previousPaths = routedWires.Take(index).Select(item => item.Path).ToList();
            DrawRoutedWire(canvas, wire.Path, previousPaths);
            if (IsSimulationPlaying && wire.ElectronDirection != 0)
                DrawElectricityDots(canvas, wire.Path, wire.ElectronDirection);

            if (wire.Owner is WireComponent owner && owner.ShowLiveProbe)
                DrawWireMeasurement(canvas, owner, wire.Path);
        }
    }

    private static int GetElectronDirection(ComponentNode fromComponent, ComponentPin fromPin, ComponentNode toComponent, ComponentPin toPin)
    {
        int fromPinIndex = fromComponent.Pins.IndexOf(fromPin);
        if (Math.Abs(fromComponent.LiveCurrent) > 1e-12 && fromPinIndex >= 0)
        {
            int conventionalDirection = Math.Sign(fromComponent.LiveCurrent) * (fromPinIndex == 0 ? -1 : 1);
            return -conventionalDirection;
        }

        int toPinIndex = toComponent.Pins.IndexOf(toPin);
        if (Math.Abs(toComponent.LiveCurrent) > 1e-12 && toPinIndex >= 0)
        {
            int conventionalDirection = Math.Sign(toComponent.LiveCurrent) * (toPinIndex == 0 ? 1 : -1);
            return -conventionalDirection;
        }
        return 0;
    }

    private static void DrawRoutedWire(ICanvas canvas, List<PointF> path, List<List<PointF>> previousPaths)
    {
        canvas.StrokeColor = Color.FromArgb("#58A6FF");
        canvas.StrokeSize = 1.7f;
        for (int segmentIndex = 0; segmentIndex < path.Count - 1; segmentIndex++)
        {
            var start = path[segmentIndex];
            var end = path[segmentIndex + 1];
            bool horizontal = MathF.Abs(start.Y - end.Y) < 0.01f;
            var crossings = FindCrossings(start, end, horizontal, previousPaths);
            float cursor = horizontal ? Math.Min(start.X, end.X) : Math.Min(start.Y, end.Y);
            float finish = horizontal ? Math.Max(start.X, end.X) : Math.Max(start.Y, end.Y);

            foreach (float crossing in crossings)
            {
                const float radius = 7f;
                float clearStart = Math.Max(cursor, crossing - radius);
                float clearEnd = Math.Min(finish, crossing + radius);
                DrawAxisLine(canvas, horizontal, start, cursor, clearStart);
                canvas.StrokeColor = Color.FromArgb("#0D1117");
                canvas.StrokeSize = 4f;
                DrawAxisLine(canvas, horizontal, start, clearStart, clearEnd);
                canvas.StrokeColor = Color.FromArgb("#58A6FF");
                canvas.StrokeSize = 1.7f;
                DrawBridge(canvas, horizontal, start, crossing, radius);
                cursor = clearEnd;
            }
            DrawAxisLine(canvas, horizontal, start, cursor, finish);
        }

        if (path.Count > 0)
        {
            canvas.FillColor = Color.FromArgb("#58A6FF");
            canvas.FillCircle(path[0].X, path[0].Y, 3f);
            canvas.FillCircle(path[^1].X, path[^1].Y, 3f);
        }
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

    private void DrawElectricityDots(ICanvas canvas, List<PointF> path, int electronDirection)
    {
        float totalLength = 0;
        for (int i = 0; i < path.Count - 1; i++)
            totalLength += MathF.Abs(path[i + 1].X - path[i].X) + MathF.Abs(path[i + 1].Y - path[i].Y);
        if (totalLength < 1f) return;

        int dotCount = Math.Clamp((int)(totalLength / 28f), 2, 6);
        for (int i = 0; i < dotCount; i++)
        {
            float norm = ((float)i / dotCount + ElectricityAnimPhase) % 1.0f;
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

        float sx = (float)(comp.ScaleX > 0 ? comp.ScaleX : 1.0);
        float sy = (float)(comp.ScaleY > 0 ? comp.ScaleY : 1.0);

        // Selection outline with resize handles
        if (selected)
        {
            float halfX = 26f * sx;
            float halfY = 26f * sy;
            const float hSz  = 5f;  // handle half-size

            // Glow border
            canvas.StrokeColor = Color.FromArgb("#79C0FF");
            canvas.StrokeSize = 1.5f;
            canvas.StrokeDashPattern = new float[] { 4, 3 };
            canvas.DrawRoundedRectangle(-halfX, -halfY, halfX * 2, halfY * 2, 4);
            canvas.StrokeDashPattern = null;

            // Resize handles at 4 corners and 4 edge midpoints
            canvas.FillColor = Color.FromArgb("#161B22");
            canvas.StrokeColor = Color.FromArgb("#79C0FF");
            canvas.StrokeSize = 1.2f;
            float[] hx = { -halfX, 0,     halfX,  -halfX, halfX,  -halfX, 0,     halfX };
            float[] hy = { -halfY, -halfY, -halfY, 0,     0,      halfY,  halfY, halfY };
            for (int i = 0; i < 8; i++)
            {
                canvas.FillRoundedRectangle(hx[i] - hSz, hy[i] - hSz, hSz * 2, hSz * 2, 2);
                canvas.DrawRoundedRectangle(hx[i] - hSz, hy[i] - hSz, hSz * 2, hSz * 2, 2);
            }

            // Rotate handle: circle to the right of the selection box
            float rHx = halfX + 14f;
            float rHy = 0f;
            canvas.FillColor = Color.FromArgb("#1C2128");
            canvas.FillCircle(rHx, rHy, RotateHandleRadius);
            canvas.StrokeColor = Color.FromArgb("#D2A8FF");
            canvas.StrokeSize = 1.5f;
            canvas.DrawCircle(rHx, rHy, RotateHandleRadius);
            // Curved arrow symbol inside rotate handle
            canvas.FontColor = Color.FromArgb("#D2A8FF");
            canvas.FontSize = 8f;
            canvas.DrawString("⟳", rHx - 5, rHy - 5, 10, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        bool isBroken = comp.IsBrokenDown;
        if (isBroken)
        {
            // Glowing red breakdown / overload halo behind the component
            canvas.FillColor = Color.FromRgba(248, 81, 73, 90);
            canvas.FillCircle(0, 0, 26 * Math.Max(sx, sy));

            // Breakdown warning indicator badge at top-right
            canvas.FillColor = Color.FromArgb("#B3261E");
            canvas.FillCircle(20 * sx, -20 * sy, 8.5f);
            canvas.FontColor = Colors.White;
            canvas.FontSize = 9f;
            canvas.DrawString("⚡", 15 * sx, -25 * sy, 10, 10, HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        bool isOn = comp switch
        {
            EESimulator.Core.Components.Electronics.LEDComponent led => led.IsOn,
            EESimulator.Core.Components.Electrical.BulbComponent bulb => bulb.IsOn,
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
        canvas.Scale(sx, sy);

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
                ComponentSymbolDrawer.DrawDigitalBlock(canvas, macroLabel, "#D2A8FF");
                break;
            case "Junction":      DrawJunction(canvas, comp); break;
            case "Label":            DrawLabel(canvas, comp.Name); break;
            case "MutualInductance": ComponentSymbolDrawer.DrawMutualInductance(canvas); break;
            case "TransmissionLine": ComponentSymbolDrawer.DrawTransmissionLine(canvas); break;
            case "VoltageDelay":     ComponentSymbolDrawer.DrawVoltageDelay(canvas); break;
            case "Clock":            ComponentSymbolDrawer.DrawClock(canvas, isOn); break;
            case "Buffer":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "BUF", "#58A6FF"); break;
            case "ControlledBuffer": ComponentSymbolDrawer.DrawDigitalBlock(canvas, "TRI", "#58A6FF"); break;
            case "EvenParity":       ComponentSymbolDrawer.DrawDigitalBlock(canvas, "EVEN", "#58A6FF"); break;
            case "OddParity":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "ODD", "#58A6FF"); break;
            case "PLA":              ComponentSymbolDrawer.DrawDigitalBlock(canvas, "PLA", "#58A6FF"); break;
            case "Multiplexer":      ComponentSymbolDrawer.DrawDigitalBlock(canvas, "MUX", "#79C0FF"); break;
            case "Demultiplexer":    ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DMX", "#79C0FF"); break;
            case "Decoder":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DEC", "#79C0FF"); break;
            case "PriorityEncoder":  ComponentSymbolDrawer.DrawDigitalBlock(canvas, "PRI", "#79C0FF"); break;
            case "BitSelector":      ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SEL", "#79C0FF"); break;
            case "DFlipFlop":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DFF", "#D2A8FF"); break;
            case "JKFlipFlop":       ComponentSymbolDrawer.DrawDigitalBlock(canvas, "JK", "#D2A8FF"); break;
            case "SRFlipFlop":       ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SR", "#D2A8FF"); break;
            case "TFlipFlop":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "TFF", "#D2A8FF"); break;
            case "Register":         ComponentSymbolDrawer.DrawDigitalBlock(canvas, "REG", "#D2A8FF"); break;
            case "Counter":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "CTR", "#D2A8FF"); break;
            case "ShiftRegister":    ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SHR", "#D2A8FF"); break;
            case "RAM":              ComponentSymbolDrawer.DrawDigitalBlock(canvas, "RAM", "#D2A8FF"); break;
            case "ROM":              ComponentSymbolDrawer.DrawDigitalBlock(canvas, "ROM", "#D2A8FF"); break;
            case "Random":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "RND", "#D2A8FF"); break;
            case "Adder":             ComponentSymbolDrawer.DrawDigitalBlock(canvas, "+", "#FFB86C"); break;
            case "Subtractor":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "−", "#FFB86C"); break;
            case "Multiplier":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "×", "#FFB86C"); break;
            case "Divider":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "÷", "#FFB86C"); break;
            case "Comparator":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "CMP", "#FFB86C"); break;
            case "Negator":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "−x", "#FFB86C"); break;
            case "Shifter":           ComponentSymbolDrawer.DrawDigitalBlock(canvas, "SHF", "#FFB86C"); break;
            case "BitAdder":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "POP", "#FFB86C"); break;
            case "BitFinder":         ComponentSymbolDrawer.DrawDigitalBlock(canvas, "FND", "#FFB86C"); break;
            case "SquareRoot":        ComponentSymbolDrawer.DrawDigitalBlock(canvas, "√", "#FFB86C"); break;
            case "AbsoluteValue":     ComponentSymbolDrawer.DrawDigitalBlock(canvas, "ABS", "#FFB86C"); break;
            case "MinMax":            ComponentSymbolDrawer.DrawDigitalBlock(canvas, "MIN", "#FFB86C"); break;
            case "Exponentiator":     ComponentSymbolDrawer.DrawDigitalBlock(canvas, "POW", "#FFB86C"); break;
            case "Button":            ComponentSymbolDrawer.DrawButton(canvas, isOn); break;
            case "DIPSwitch":         ComponentSymbolDrawer.DrawDigitalBlock(canvas, "DIP", "#56D364"); break;
            case "SevenSegment":      ComponentSymbolDrawer.DrawSevenSegment(canvas, comp.CustomProperties.TryGetValue("RenderedSegments", out var segs) ? Convert.ToInt32(segs) : 0); break;
            case "HexDigit":          ComponentSymbolDrawer.DrawHexDigit(canvas, comp.CustomProperties.TryGetValue("RenderedValue", out var hexValue) ? Convert.ToInt32(hexValue) : 0); break;
            case "LedBar":            ComponentSymbolDrawer.DrawLedBar(canvas, comp.CustomProperties.TryGetValue("RenderedValue", out var ledBarValue) ? Convert.ToInt64(ledBarValue) : 0, comp is EESimulator.Core.Components.Electronics.LedBarComponent ledBar ? ledBar.LedCount : 8); break;
            case "RGBLED":            ComponentSymbolDrawer.DrawRgbLed(canvas, comp.CustomProperties.TryGetValue("RenderedRgb", out var rgb) ? Convert.ToInt32(rgb) : 0); break;
            case "DotMatrix":         ComponentSymbolDrawer.DrawDotMatrix(canvas, comp.CustomProperties.TryGetValue("RenderedRows", out var matrixRows) ? Convert.ToInt64(matrixRows) : 0, comp.CustomProperties.TryGetValue("RenderedColumns", out var matrixCols) ? Convert.ToInt64(matrixCols) : 0, comp is EESimulator.Core.Components.Electronics.DotMatrixComponent matrix ? matrix.Rows : 7, comp is EESimulator.Core.Components.Electronics.DotMatrixComponent matrixColumns ? matrixColumns.Columns : 5); break;
            case "Joystick":          ComponentSymbolDrawer.DrawJoystick(canvas); break;
            case "Keyboard":          ComponentSymbolDrawer.DrawDigitalBlock(canvas, "KEY", "#56D364"); break;
            case "TTY":               ComponentSymbolDrawer.DrawTty(canvas, comp is EESimulator.Core.Components.Electronics.TtyComponent tty ? tty.Text : string.Empty); break;
            case "Video":             ComponentSymbolDrawer.DrawVideo(canvas, comp.CustomProperties.TryGetValue("RenderedRgb", out var videoRgb) ? Convert.ToInt32(videoRgb) : 0); break;
            case "RealTimeClock":
                string clockLabel = comp.CustomProperties.TryGetValue("RenderedHours", out var clockHours) && comp.CustomProperties.TryGetValue("RenderedMinutes", out var clockMinutes)
                    ? $"{Convert.ToInt32(clockHours):00}:{Convert.ToInt32(clockMinutes):00}"
                    : "RTC";
                ComponentSymbolDrawer.DrawDigitalBlock(canvas, clockLabel, "#56D364");
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
            -30, 22, 60, 14, HorizontalAlignment.Center, VerticalAlignment.Top);

        // Live probe HUD (Eye icon probe enabled)
        if ((comp.ShowLiveProbe || LiveMeasurementsVisible) && comp.ComponentType != "Wire")
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

        // Draw small circle to nodes/pins on all components in viewport with connection degree coloring
        foreach (var pin in comp.Pins)
        {
            float px = (float)pin.RelativeX * sx;
            float py = (float)pin.RelativeY * sy;
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

    private void DrawJunction(ICanvas canvas, ComponentNode comp)
    {
        var pin = comp.Pins.FirstOrDefault();
        int connCount = pin != null ? GetPinConnectionCount(comp, pin) : 0;
        Color c = GetNodeConnectionColor(connCount);
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
