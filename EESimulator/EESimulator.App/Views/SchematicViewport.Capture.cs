using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia;
using SkiaSharp;

namespace EESimulator.App.Views;

public sealed record SchematicCapturedFrame(byte[] Png, byte[] RgbPixels);

public partial class SchematicViewport
{
    // ── Recentred capture (all components visible, like sidebar Recentre button) ──

    public byte[] CaptureRecenteredPng(int width, int height, WaveformViewport? waveformDrawer = null)
    {
        using var export = CreateRecenteredExport(width, height, includeAnimation: false, waveformDrawer);
        using var stream = new MemoryStream();
        export.WriteToStream(stream);
        return stream.ToArray();
    }

    public SchematicCapturedFrame CaptureRecenteredFrame(
        int width,
        int height,
        bool includePng = true,
        WaveformViewport? waveformDrawer = null,
        string? subtitleTextOverride = null)
    {
        using var export = CreateRecenteredExport(width, height, includeAnimation: true, waveformDrawer, subtitleTextOverride);
        byte[] pngData = Array.Empty<byte>();
        if (includePng)
        {
            using var pngStream = new MemoryStream();
            export.WriteToStream(pngStream);
            pngData = pngStream.ToArray();
        }

        byte[] pixels = ExtractRgbPixels(export.Bitmap, width, height);
        return new SchematicCapturedFrame(pngData, pixels);
    }

    // ── Current-view capture (exactly what the user sees on screen) ──────────

    public byte[] CaptureCurrentViewPng(int width, int height, WaveformViewport? waveformDrawer = null)
    {
        using var export = CreateCurrentViewExport(width, height, (float)Width, (float)Height, includeAnimation: false, waveformDrawer);
        using var stream = new MemoryStream();
        export.WriteToStream(stream);
        return stream.ToArray();
    }

    public SchematicCapturedFrame CaptureCurrentViewFrame(
        int width,
        int height,
        bool includePng = true,
        WaveformViewport? waveformDrawer = null,
        string? subtitleTextOverride = null)
        => CaptureCurrentViewFrame(width, height, (float)Width, (float)Height, includePng, waveformDrawer, subtitleTextOverride);

    public SchematicCapturedFrame CaptureCurrentViewFrame(
        int width,
        int height,
        float viewportWidth,
        float viewportHeight,
        bool includePng = true,
        WaveformViewport? waveformDrawer = null,
        string? subtitleTextOverride = null)
    {
        using var export = CreateCurrentViewExport(width, height, viewportWidth, viewportHeight, includeAnimation: true, waveformDrawer, subtitleTextOverride);
        byte[] pngData = Array.Empty<byte>();
        if (includePng)
        {
            using var pngStream = new MemoryStream();
            export.WriteToStream(pngStream);
            pngData = pngStream.ToArray();
        }

        byte[] pixels = ExtractRgbPixels(export.Bitmap, width, height);
        return new SchematicCapturedFrame(pngData, pixels);
    }

    private static byte[] ExtractRgbPixels(SKBitmap bitmap, int width, int height)
    {
        var pixels = new byte[width * height * 3];
        ReadOnlySpan<byte> span = bitmap.GetPixelSpan();
        int rowBytes = bitmap.RowBytes;
        bool isBgra = bitmap.ColorType == SKColorType.Bgra8888;
        int rOffset = isBgra ? 2 : 0;
        int gOffset = 1;
        int bOffset = isBgra ? 0 : 2;
        int bytesPerPixel = Math.Max(3, bitmap.BytesPerPixel);

        int destOffset = 0;
        for (int y = 0; y < height; y++)
        {
            int srcRowStart = y * rowBytes;
            for (int x = 0; x < width; x++)
            {
                int srcOffset = srcRowStart + x * bytesPerPixel;
                if (srcOffset + 2 < span.Length)
                {
                    pixels[destOffset++] = span[srcOffset + rOffset];
                    pixels[destOffset++] = span[srcOffset + gOffset];
                    pixels[destOffset++] = span[srcOffset + bOffset];
                }
                else
                {
                    pixels[destOffset++] = 0;
                    pixels[destOffset++] = 0;
                    pixels[destOffset++] = 0;
                }
            }
        }
        return pixels;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private SkiaBitmapExportContext CreateRecenteredExport(
        int width,
        int height,
        bool includeAnimation,
        WaveformViewport? waveformDrawer = null,
        string? subtitleTextOverride = null)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Capture dimensions must be positive.");

        float drawerHeight = waveformDrawer != null ? Math.Min(height * 0.40f, 260f) : 0f;
        float schematicHeight = height - drawerHeight;

        var components = Circuit?.FindChildren<EESimulator.Core.Models.ComponentNode>()
            .Where(component => component is not EESimulator.Core.Components.General.WireComponent)
            .ToArray() ?? Array.Empty<EESimulator.Core.Models.ComponentNode>();
        if (components.Length == 0)
        {
            var emptyExport = new SkiaBitmapExportContext(width, height, 1f, 96, transparent: false);
            try
            {
                DrawWithCamera(emptyExport.Canvas, new RectF(0, 0, width, schematicHeight),
                    width / 2f, schematicHeight / 2f, 1f, false, subtitleTextOverride);
                if (waveformDrawer != null)
                    RenderWaveformDrawerOverlay(emptyExport.Canvas, width, height, schematicHeight, drawerHeight, waveformDrawer);
            }
            catch
            {
                emptyExport.Dispose();
                throw;
            }
            return emptyExport;
        }

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
            MathF.Max(schematicHeight - 2 * padding, 1f) / contentHeight);
        var export = new SkiaBitmapExportContext(width, height, 1f, 96, transparent: false);
        float panX = width / 2f - ((minX + maxX) / 2f) * zoom;
        float panY = schematicHeight / 2f - ((minY + maxY) / 2f) * zoom;
        try
        {
            DrawWithCamera(export.Canvas, new RectF(0, 0, width, schematicHeight),
                panX, panY, zoom, includeAnimation && IsSimulationPlaying, subtitleTextOverride);
            if (waveformDrawer != null)
                RenderWaveformDrawerOverlay(export.Canvas, width, height, schematicHeight, drawerHeight, waveformDrawer);
        }
        catch
        {
            export.Dispose();
            throw;
        }
        return export;
    }

    /// <summary>
    /// Captures the schematic at exactly the user's current pan/zoom —
    /// the same viewport rectangle they are looking at right now.
    /// </summary>
    private SkiaBitmapExportContext CreateCurrentViewExport(
        int width,
        int height,
        float viewportWidth,
        float viewportHeight,
        bool includeAnimation,
        WaveformViewport? waveformDrawer = null,
        string? subtitleTextOverride = null)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Capture dimensions must be positive.");

        float drawerHeight = waveformDrawer != null ? Math.Min(height * 0.40f, 260f) : 0f;
        float schematicHeight = height - drawerHeight;

        var export = new SkiaBitmapExportContext(width, height, 1f, 96, transparent: false);

        // Scale the current logical viewport (_panX/_panY/_zoom computed for the
        // live canvas size) to the requested export size while preserving the same
        // visible centre and apparent zoom density.
        float scaleX = width / MathF.Max(1f, viewportWidth);
        float scaleY = schematicHeight / MathF.Max(1f, viewportHeight);
        float scale  = MathF.Min(scaleX, scaleY);

        // Centre the same world-space centre in the export canvas.
        float panX = _panX;
        float panY = _panY;
        float zoom = _zoom;
        float worldCentreX = (viewportWidth / 2f - panX) / zoom;
        float worldCentreY = (viewportHeight / 2f - panY) / zoom;
        float exportZoom = zoom * scale;
        float exportPanX = width / 2f - worldCentreX * exportZoom;
        float exportPanY = schematicHeight / 2f - worldCentreY * exportZoom;

        try
        {
            DrawWithCamera(export.Canvas, new RectF(0, 0, width, schematicHeight),
                exportPanX, exportPanY, exportZoom, includeAnimation && IsSimulationPlaying, subtitleTextOverride);
            if (waveformDrawer != null)
                RenderWaveformDrawerOverlay(export.Canvas, width, height, schematicHeight, drawerHeight, waveformDrawer);
        }
        catch
        {
            export.Dispose();
            throw;
        }
        return export;
    }

    private static void RenderWaveformDrawerOverlay(ICanvas canvas, int width, int height, float schematicHeight, float drawerHeight, WaveformViewport waveformDrawer)
    {
        canvas.SaveState();
        try
        {
            float drawerTop = schematicHeight;
            // Oscilloscope dark background
            canvas.FillColor = Color.FromArgb("#0A0E14");
            canvas.FillRectangle(0, drawerTop, width, drawerHeight);

            // Header bar
            const float headerH = 24f;
            canvas.FillColor = Color.FromArgb("#161B22");
            canvas.FillRectangle(0, drawerTop, width, headerH);

            canvas.StrokeColor = Color.FromArgb("#30363D");
            canvas.StrokeSize = 1;
            canvas.DrawLine(0, drawerTop, width, drawerTop);
            canvas.DrawLine(0, drawerTop + headerH, width, drawerTop + headerH);

            canvas.FontColor = Color.FromArgb("#58A6FF");
            canvas.FontSize = 11;
            canvas.DrawString("📈 Waveforms (Oscilloscope)", 12, drawerTop + 4, width - 24, 16, HorizontalAlignment.Left, VerticalAlignment.Center);

            // Oscilloscope plot area
            canvas.Translate(0, drawerTop + headerH);
            waveformDrawer.Draw(canvas, new RectF(0, 0, width, drawerHeight - headerH));
        }
        finally
        {
            canvas.RestoreState();
        }
    }
}
