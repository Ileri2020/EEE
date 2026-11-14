using Microsoft.Maui.Graphics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>
/// Renders the schematic circuit on a MAUI GraphicsView canvas.
/// 
/// Viewport actions are selected from the workspace toolbar. Canvas taps select
/// objects or place the two endpoints of a wire; the Pan and Move tools use dragging.
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

    /// <summary>Controls whether electricity movements (animated current dots) are rendered.</summary>
    public bool IsSimulationPlaying { get; set; } = false;
    public float ElectricityAnimPhase { get; set; } = 0.0f;

    // ── Pan / zoom ───────────────────────────────────────────────────────────
    private float _panX, _panY;
    private float _zoom = 1.0f;
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 5.0f;
    private float _lastPanX, _lastPanY;
    private float _lastPinchScale = 1f;
    private bool _isPinching;
    private const float GridSize = 20f;

    // ── Events ───────────────────────────────────────────────────────────────
    public event EventHandler<ComponentNode>? ComponentSelected;
    public event EventHandler<ComponentNode>? LogicInputToggled;
    public event EventHandler<ConnectionNode>? ConnectionSelected;
    public event EventHandler<(ComponentNode Component, ComponentPin Pin)>? PinSelected;
    public event EventHandler<(ComponentNode Component, double OldX, double OldY, double NewX, double NewY)>? ComponentMoveCommitted;
    public event EventHandler<ComponentNode>? ComponentRotateRequested;
    public event EventHandler<(ComponentNode From, ComponentPin FromPin,
                               ComponentNode To,   ComponentPin ToPin)>? WireConnected;
    public event EventHandler<JunctionComponent>? WireCutIntoNode;

    /// <summary>Active tool selected in the workspace sidebar.</summary>
    public string ActiveTool { get; set; } = "Select";

    public void SetActiveTool(string tool)
    {
        ActiveTool = tool;
        if (!tool.Equals("Wire", StringComparison.OrdinalIgnoreCase))
            CancelWire();
        Invalidate();
    }

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

    private ComponentNode? _pressedComp;

    // Component drag/move
    private bool   _isMovingComponent;
    private float _componentDragStartX, _componentDragStartY;
    private float _componentDragStartTotalX, _componentDragStartTotalY;
    private float _lastPanTotalX, _lastPanTotalY;
    private bool _componentDragMoved;

    // Wire draw
    private bool           _isDrawingWire;
    private ComponentNode? _wireSourceComp;
    private ComponentPin?  _wireSourcePin;
    private PointF         _wireCursorWorld;

    // Background pan (swipe on mobile / left-click drag on desktop over empty canvas)
    private bool   _isBackgroundPanning;
    private float  _bgPanStartX,  _bgPanStartY;   // _panX / _panY at the start of the gesture
    private PointF _bgPanStartScreen;              // screen-space position where pointer pan began (desktop)

    public SchematicViewport()
    {
        Drawable        = this;
        BackgroundColor = Color.FromArgb("#0D1117");

        // Drag gestures pan the viewport or move the selected component, depending on the active tool.
        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += OnPan;
        GestureRecognizers.Add(panGesture);

        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinchGesture);

        // Canvas taps are interpreted by the active toolbar tool.
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnTapped;
        GestureRecognizers.Add(tapGesture);

        // Pointer events handle mouse-only controls; taps and drags use the shared gestures below.
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerPressed += OnPointerPressed;
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
            vp.InvalidateWireGeometryCache();
            vp.Invalidate();
        }
    }

    private void OnCircuitStructureChanged(object? sender, EventArgs e)
        => Dispatcher.Dispatch(() =>
        {
            InvalidateWireGeometryCache();
            Invalidate();
        });

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



    // ── Windows native pointer interaction ─────────────────────────────────
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _nativeInputElement;

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
        if (_nativeInputElement != null)
        {
            _nativeInputElement.PointerPressed -= OnNativePtrPressed;
            _nativeInputElement.PointerReleased -= OnNativePtrReleased;
            _nativeInputElement = null;
        }

        base.OnHandlerChanged();

        if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement nativeElement)
        {
            _nativeInputElement = nativeElement;
            nativeElement.PointerPressed += OnNativePtrPressed;
            nativeElement.PointerReleased += OnNativePtrReleased;
        }
    }

    private void OnNativePtrPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(sender as Microsoft.UI.Xaml.UIElement).Properties.IsRightButtonPressed)
            e.Handled = true;
    }

    private void OnNativePtrReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isStretchingComponent = false;
        _stretchHandleIndex = -1;
    }
#endif

    // ── Cross-platform gesture handlers ───────────────────────────────────
    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        if (_isPinching)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _lastPanX = _panX;
                _lastPanY = _panY;
                _lastPanTotalX = 0;
                _lastPanTotalY = 0;
                _isMovingComponent = ActiveTool.Equals("Move", StringComparison.OrdinalIgnoreCase) &&
                    _selectedComponent != null;
                _pressedComp = _isMovingComponent ? _selectedComponent : null;
                if (_pressedComp != null)
                {
                    _componentDragStartX = (float)_pressedComp.X;
                    _componentDragStartY = (float)_pressedComp.Y;
                    _componentDragStartTotalX = 0;
                    _componentDragStartTotalY = 0;
                    _componentDragMoved = false;
                }

                // Detect background pan: starts when no component is under the touch point
                // and we are not in Wire/Cut/Move tool modes (those need their own touch logic).\n                // On desktop, _isBackgroundPanning may already be set true by OnPointerPressed
                // (which has the precise click coordinates for hit-testing). On mobile, the
                // PanGestureRecognizer does not give us start coordinates, so we allow panning
                // whenever no component drag is active and the tool isn't a wire/cut mode.
                if (!_isMovingComponent &&
                    !ActiveTool.Equals("Pan", StringComparison.OrdinalIgnoreCase) &&
                    !ActiveTool.Equals("Wire", StringComparison.OrdinalIgnoreCase) &&
                    !ActiveTool.Equals("Cut", StringComparison.OrdinalIgnoreCase) &&
                    !_isDrawingWire &&
                    !_isBackgroundPanning)   // desktop already set this in OnPointerPressed
                {
                    // Mobile: allow pan over empty canvas.
                    // We cannot hit-test here (no start position), so we permit it and
                    // rely on the fact that component drags are handled by _isMovingComponent.
                    _isBackgroundPanning = true;
                    _bgPanStartX = _panX;
                    _bgPanStartY = _panY;
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
                else if (ActiveTool.Equals("Pan", StringComparison.OrdinalIgnoreCase) ||
                         _isBackgroundPanning)
                {
                    // On desktop, background panning is driven by OnPointerMoved for
                    // sub-pixel accuracy; skip PanGestureRecognizer updates here so
                    // both handlers don't fight over _panX / _panY.
#if !WINDOWS
                    _panX = _lastPanX + (float)e.TotalX;
                    _panY = _lastPanY + (float)e.TotalY;
                    Invalidate();
#else
                    if (ActiveTool.Equals("Pan", StringComparison.OrdinalIgnoreCase))
                    {
                        _panX = _lastPanX + (float)e.TotalX;
                        _panY = _lastPanY + (float)e.TotalY;
                        Invalidate();
                    }
                    // Background pan on Windows: handled by OnPointerMoved instead.
#endif
                }
                _lastPanTotalX = (float)e.TotalX;
                _lastPanTotalY = (float)e.TotalY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isBackgroundPanning = false;
                FinishComponentDrag();
                break;
        }
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _isPinching = true;
                _lastPinchScale = 1f;
                _isBackgroundPanning = false;
                FinishComponentDrag();
                break;

            case GestureStatus.Running:
                if (e.Scale <= 0 || !float.IsFinite((float)e.Scale))
                    return;

                float scaleDelta = (float)e.Scale / _lastPinchScale;
                _lastPinchScale = (float)e.Scale;
                ZoomAt((float)(e.ScaleOrigin.X * Width), (float)(e.ScaleOrigin.Y * Height), scaleDelta);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isPinching = false;
                _lastPinchScale = 1f;
                break;
        }
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos == null || Circuit == null) return;

        var w = ToWorld((float)pos.Value.X, (float)pos.Value.Y);

        if (ActiveTool.Equals("Cut", StringComparison.OrdinalIgnoreCase))
        {
            if (TryCutWireToNode(w))
            {
                Invalidate();
                return;
            }
        }

        var pin = HitTestPin(w);
        if (ActiveTool.Equals("Wire", StringComparison.OrdinalIgnoreCase))
        {
            if (pin != null)
            {
                if (!_isDrawingWire)
                {
                    _isDrawingWire = true;
                    _wireSourceComp = pin.Value.Component;
                    _wireSourcePin = pin.Value.Pin;
                    _wireCursorWorld = SchematicWireRouter.GetPinWorldPosition(_wireSourceComp, _wireSourcePin);
                    _selectedComponent = pin.Value.Component;
                    UpdateRotateHandle();
                    PinSelected?.Invoke(this, pin.Value);
                    ComponentSelected?.Invoke(this, pin.Value.Component);
                    return;
                }

                if (_wireSourceComp == pin.Value.Component && _wireSourcePin == pin.Value.Pin)
                {
                    CancelWire();
                    return;
                }

                _wireCursorWorld = w;
                TryCompleteWire(w);
                return;
            }

            if (_isDrawingWire)
                TryCompleteWire(w);
            return;
        }

        if (!ActiveTool.Equals("Select", StringComparison.OrdinalIgnoreCase))
            return;

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

        if (_selectedComponent is LogicInputComponent logicInput)
        {
            logicInput.Toggle();
            LogicInputToggled?.Invoke(this, logicInput);
        }

        Invalidate();
        ComponentSelected?.Invoke(this, _selectedComponent);
    }

}
