using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using EESimulator.Core.Models;

namespace EESimulator.App.Views;

/// <summary>
/// Renders the vector schematic symbol of a component inside the sidebar palette tiles.
/// Draws via ComponentSymbolDrawer (same routines used by the main viewport) so icons
/// are always pixel-perfect and match what appears on the canvas.
/// </summary>
public class ComponentIconView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ComponentTypeProperty =
        BindableProperty.Create(
            nameof(ComponentType),
            typeof(string),
            typeof(ComponentIconView),
            string.Empty,
            propertyChanged: (b, _, __) => ((ComponentIconView)b).Invalidate());

    public static readonly BindableProperty IsOnProperty =
        BindableProperty.Create(
            nameof(IsOn),
            typeof(bool),
            typeof(ComponentIconView),
            false,
            propertyChanged: (b, _, __) => ((ComponentIconView)b).Invalidate());

    public static readonly BindableProperty ComponentProperty =
        BindableProperty.Create(nameof(Component), typeof(ComponentNode), typeof(ComponentIconView), null,
            propertyChanged: (b, oldValue, newValue) => ((ComponentIconView)b).OnComponentChanged(
                oldValue as ComponentNode, newValue as ComponentNode));

    public string ComponentType
    {
        get => (string)GetValue(ComponentTypeProperty);
        set => SetValue(ComponentTypeProperty, value);
    }

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public ComponentNode? Component
    {
        get => (ComponentNode?)GetValue(ComponentProperty);
        set => SetValue(ComponentProperty, value);
    }

    public ComponentIconView()
    {
        Drawable         = this;
        BackgroundColor  = Colors.Transparent;
        InputTransparent = true; // Let parent Border receive tap gestures
    }

    // Force redraw once the view has been measured and laid out
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && height > 0)
            Invalidate();
    }

    // Also redraw when the view is attached to a live visual tree
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent != null)
            Dispatcher?.Dispatch(Invalidate);
    }

    public new void Invalidate()
    {
        base.Invalidate();
#if WINDOWS
        try
        {
            if (Handler?.PlatformView is Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl cc)
            {
                cc.Invalidate();
            }
        }
        catch { }
#endif
    }

    private void OnComponentChanged(ComponentNode? oldComponent, ComponentNode? newComponent)
    {
        if (oldComponent != null) oldComponent.OnModified -= OnComponentModified;
        if (newComponent != null) newComponent.OnModified += OnComponentModified;
        Invalidate();
    }

    private void OnComponentModified(object? sender, NodeModifiedEventArgs e)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(Invalidate);
        else
            Invalidate();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (string.IsNullOrEmpty(ComponentType))
            return;

        float w = dirtyRect.Width > 0 ? dirtyRect.Width : (float)(Width > 0 ? Width : (WidthRequest > 0 ? WidthRequest : 44));
        float h = dirtyRect.Height > 0 ? dirtyRect.Height : (float)(Height > 0 ? Height : (HeightRequest > 0 ? HeightRequest : 44));
        if (w <= 0 || h <= 0) return;

        canvas.SaveState();

        // Centre the symbol within the tile
        float cx = w / 2f;
        float cy = h / 2f;
        canvas.Translate(cx, cy);

        // Scale so the ±20 world-unit symbols fit in the tile with 4px padding
        float scaleX = (w - 6f) / 44f;
        float scaleY = (h - 6f) / 44f;
        float scale  = Math.Min(scaleX, scaleY);
        canvas.Scale(scale, scale);

        bool componentIsOn = Component switch
        {
            EESimulator.Core.Components.Electronics.LEDComponent led => led.IsOn,
            EESimulator.Core.Components.Electrical.BulbComponent bulb => bulb.IsOn,
            EESimulator.Core.Components.Electronics.LogicInputComponent input => input.State,
            EESimulator.Core.Components.Electronics.LogicOutputComponent output => output.IsOn,
            EESimulator.Core.Components.Electronics.ClockComponent clock => clock.State,
            EESimulator.Core.Components.Electronics.PinComponent pin => pin.Value > 0,
            EESimulator.Core.Components.Electronics.ConstantComponent constant => constant.Value > 0,
            _ => IsOn
        };
        switch (Component)
        {
            case EESimulator.Core.Components.Electronics.SevenSegmentComponent seven:
                ComponentSymbolDrawer.DrawSevenSegment(canvas, seven.CustomProperties.TryGetValue("RenderedSegments", out var segments) ? Convert.ToInt32(segments) : 0);
                break;
            case EESimulator.Core.Components.Electronics.HexDigitComponent hex:
                ComponentSymbolDrawer.DrawHexDigit(canvas, hex.CustomProperties.TryGetValue("RenderedValue", out var hexValue) ? Convert.ToInt32(hexValue) : 0);
                break;
            case EESimulator.Core.Components.Electronics.LedBarComponent bar:
                ComponentSymbolDrawer.DrawLedBar(canvas, bar.CustomProperties.TryGetValue("RenderedValue", out var barValue) ? Convert.ToInt64(barValue) : 0, bar.LedCount);
                break;
            case EESimulator.Core.Components.Electronics.RgbLedComponent rgb:
                ComponentSymbolDrawer.DrawRgbLed(canvas, rgb.CustomProperties.TryGetValue("RenderedRgb", out var rgbValue) ? Convert.ToInt32(rgbValue) : 0);
                break;
            case EESimulator.Core.Components.Electronics.DotMatrixComponent matrix:
                ComponentSymbolDrawer.DrawDotMatrix(canvas,
                    matrix.CustomProperties.TryGetValue("RenderedRows", out var rows) ? Convert.ToInt64(rows) : 0,
                    matrix.CustomProperties.TryGetValue("RenderedColumns", out var columns) ? Convert.ToInt64(columns) : 0,
                    matrix.Rows, matrix.Columns);
                break;
            case EESimulator.Core.Components.Electronics.VideoComponent video:
                ComponentSymbolDrawer.DrawVideo(canvas, video.CustomProperties.TryGetValue("RenderedRgb", out var videoRgb) ? Convert.ToInt32(videoRgb) : 0);
                break;
            default:
                ComponentSymbolDrawer.DrawSymbol(canvas, ComponentType, "", componentIsOn);
                break;
        }

        canvas.RestoreState();
    }
}
