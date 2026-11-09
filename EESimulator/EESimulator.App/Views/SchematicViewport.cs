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
public partial class SchematicViewport : GraphicsView, IDrawable
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
    public event EventHandler<ConnectionNode>? ConnectionSelected;
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
    private ConnectionNode? _selectedConnection;
    private (ComponentNode Component, ComponentPin Pin)? _selectedPin;
    private readonly List<RectF> _liveMeasurementBadgeBounds = new();

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
    private float _stretchScaleWidth = 26f;
    private float _stretchScaleHeight = 26f;

    private static float GetSelectionHalfWidth(ComponentNode component)
        => (float)AdaptiveComponentLayout.Get(component).HalfWidth;

    private static float GetSelectionHalfHeight(ComponentNode component)
        => (float)AdaptiveComponentLayout.Get(component).HalfHeight;

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
    /// The handle follows the current component bounds with a fixed offset.
    /// </summary>
    private void UpdateRotateHandle()
    {
        if (_selectedComponent == null)
        {
            _rotateHandleVisible = false;
            return;
        }

        float localX = GetSelectionHalfWidth(_selectedComponent) + 14f;
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

        float halfX = GetSelectionHalfWidth(_selectedComponent);
        float halfY = GetSelectionHalfHeight(_selectedComponent);

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
            _selectedConnection = null;
            _selectedPin       = pin;
            _selectedComponent = pin.Value.Component;
            UpdateRotateHandle();
            Invalidate();
            PinSelected?.Invoke(this, pin.Value);
            ComponentSelected?.Invoke(this, pin.Value.Component);
            return;
        }
        var selectedConnection = HitTestConnection(w);
        if (selectedConnection != null)
        {
            _selectedPin = null;
            _selectedComponent = null;
            _selectedConnection = selectedConnection;
            _rotateHandleVisible = false;
            ConnectionSelected?.Invoke(this, selectedConnection);
            Invalidate();
            return;
        }
        _selectedPin       = null;
        _selectedComponent = HitTest(w);
        _selectedConnection = null;
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

}
