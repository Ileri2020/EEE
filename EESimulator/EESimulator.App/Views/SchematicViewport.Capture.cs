using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using SkiaSharp;

namespace EESimulator.App.Views;

public sealed record SchematicCapturedFrame(byte[] Png, byte[] RgbPixels);

public partial class SchematicViewport
{
    // ── Recentred capture (all components visible, like sidebar Recentre button) ──

    public byte[] CaptureRecenteredPng(int width, int height)
    {
        using var export = CreateRecenteredExport(width, height);
        using var stream = new MemoryStream();
        export.WriteToStream(stream);
        return stream.ToArray();
    }

    public SchematicCapturedFrame CaptureRecenteredFrame(int width, int height)
    {
        using var export = CreateRecenteredExport(width, height);
        using var pngStream = new MemoryStream();
        export.WriteToStream(pngStream);

        SKBitmap bitmap = export.Bitmap;
        var pixels = new byte[width * height * 3];
        int offset = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                pixels[offset++] = color.Red;
                pixels[offset++] = color.Green;
                pixels[offset++] = color.Blue;
            }
        }
        return new SchematicCapturedFrame(pngStream.ToArray(), pixels);
    }

    // ── Current-view capture (exactly what the user sees on screen) ──────────

    public byte[] CaptureCurrentViewPng(int width, int height)
    {
        using var export = CreateCurrentViewExport(width, height);
        using var stream = new MemoryStream();
        export.WriteToStream(stream);
        return stream.ToArray();
    }

    public SchematicCapturedFrame CaptureCurrentViewFrame(int width, int height)
    {
        using var export = CreateCurrentViewExport(width, height);
        using var pngStream = new MemoryStream();
        export.WriteToStream(pngStream);

        SKBitmap bitmap = export.Bitmap;
        var pixels = new byte[width * height * 3];
        int offset = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                pixels[offset++] = color.Red;
                pixels[offset++] = color.Green;
                pixels[offset++] = color.Blue;
            }
        }
        return new SchematicCapturedFrame(pngStream.ToArray(), pixels);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private SkiaBitmapExportContext CreateRecenteredExport(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Capture dimensions must be positive.");

        var components = Circuit?.FindChildren<EESimulator.Core.Models.ComponentNode>()
            .Where(component => component is not EESimulator.Core.Components.General.WireComponent)
            .ToArray() ?? Array.Empty<EESimulator.Core.Models.ComponentNode>();
        if (components.Length == 0)
            throw new InvalidOperationException("There are no schematic components to capture.");

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

        const float padding = 32f;
        float contentWidth = MathF.Max(maxX - minX, 1f);
        float contentHeight = MathF.Max(maxY - minY, 1f);
        float zoom = MathF.Min(
            MathF.Max(width - 2 * padding, 1f) / contentWidth,
            MathF.Max(height - 2 * padding, 1f) / contentHeight);
        float oldPanX = _panX;
        float oldPanY = _panY;
        float oldZoom = _zoom;
        bool oldPlaying = IsSimulationPlaying;

        var export = new SkiaBitmapExportContext(width, height, 1f, 96, transparent: false);
        _panX = width / 2f - ((minX + maxX) / 2f) * zoom;
        _panY = height / 2f - ((minY + maxY) / 2f) * zoom;
        _zoom = zoom;
        IsSimulationPlaying = false;
        try
        {
            Draw(export.Canvas, new RectF(0, 0, width, height));
        }
        catch
        {
            export.Dispose();
            throw;
        }
        finally
        {
            _panX = oldPanX;
            _panY = oldPanY;
            _zoom = oldZoom;
            IsSimulationPlaying = oldPlaying;
        }
        return export;
    }

    /// <summary>
    /// Captures the schematic at exactly the user's current pan/zoom —
    /// the same viewport rectangle they are looking at right now.
    /// </summary>
    private SkiaBitmapExportContext CreateCurrentViewExport(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Capture dimensions must be positive.");

        bool oldPlaying = IsSimulationPlaying;
        var export = new SkiaBitmapExportContext(width, height, 1f, 96, transparent: false);

        // Scale the current logical viewport (_panX/_panY/_zoom computed for the
        // live canvas size) to the requested export size while preserving the same
        // visible centre and apparent zoom density.
        float scaleX = width  / MathF.Max(1f, (float)Width);
        float scaleY = height / MathF.Max(1f, (float)Height);
        float scale  = MathF.Min(scaleX, scaleY);

        float oldPanX = _panX;
        float oldPanY = _panY;
        float oldZoom = _zoom;
        IsSimulationPlaying = false;

        // Centre the same world-space centre in the export canvas.
        float viewportWidth = (float)Width;
        float viewportHeight = (float)Height;
        float worldCentreX = (viewportWidth / 2f - _panX) / _zoom;
        float worldCentreY = (viewportHeight / 2f - _panY) / _zoom;
        float exportZoom   = _zoom * scale;
        _panX = width  / 2f - worldCentreX * exportZoom;
        _panY = height / 2f - worldCentreY * exportZoom;
        _zoom = exportZoom;

        try
        {
            Draw(export.Canvas, new RectF(0, 0, width, height));
        }
        catch
        {
            export.Dispose();
            throw;
        }
        finally
        {
            _panX = oldPanX;
            _panY = oldPanY;
            _zoom = oldZoom;
            IsSimulationPlaying = oldPlaying;
        }
        return export;
    }
}
