using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

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

        ComponentSymbolDrawer.DrawSymbol(canvas, ComponentType, "", IsOn);

        canvas.RestoreState();
    }
}
