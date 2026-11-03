using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace EESimulator.App.Views;

/// <summary>Renders sanitized, user-provided SVG icon shapes without creating a native WebView.</summary>
public sealed class CustomSvgIconView : GraphicsView, IDrawable
{
    public static readonly BindableProperty SvgProperty = BindableProperty.Create(
        nameof(Svg), typeof(string), typeof(CustomSvgIconView), string.Empty,
        propertyChanged: (bindable, _, _) => ((CustomSvgIconView)bindable).Invalidate());

    public string Svg
    {
        get => (string)GetValue(SvgProperty);
        set => SetValue(SvgProperty, value);
    }

    public CustomSvgIconView()
    {
        Drawable = this;
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (string.IsNullOrWhiteSpace(Svg) || dirtyRect.Width <= 0 || dirtyRect.Height <= 0) return;
        try
        {
            var root = XDocument.Parse(Svg).Root;
            if (root == null || root.Name.LocalName != "svg") return;
            var viewBox = (string?)root.Attribute("viewBox") ?? "0 0 64 48";
            var box = viewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            if (box.Length != 4 || box[2] <= 0 || box[3] <= 0) return;

            canvas.SaveState();
            float scale = Math.Min(dirtyRect.Width / box[2], dirtyRect.Height / box[3]);
            float offsetX = dirtyRect.X + (dirtyRect.Width - box[2] * scale) / 2f - box[0] * scale;
            float offsetY = dirtyRect.Y + (dirtyRect.Height - box[3] * scale) / 2f - box[1] * scale;
            canvas.Translate(offsetX, offsetY);
            canvas.Scale(scale, scale);

            foreach (var element in root.Descendants())
                DrawElement(canvas, element);
            canvas.RestoreState();
        }
        catch
        {
            // Invalid or unsupported user SVGs do not prevent the palette page from loading.
        }
    }

    private static void DrawElement(ICanvas canvas, XElement element)
    {
        string? fill = (string?)element.Attribute("fill");
        string? stroke = (string?)element.Attribute("stroke");
        if (fill is "none") fill = null;
        if (stroke is "none") stroke = null;
        canvas.FillColor = ParseColor(fill, "#58A6FF");
        canvas.StrokeColor = ParseColor(stroke, "#58A6FF");
        canvas.StrokeSize = Number(element, "stroke-width", 2f);
        float x = Number(element, "x"), y = Number(element, "y");

        switch (element.Name.LocalName)
        {
            case "rect":
                float width = Number(element, "width"), height = Number(element, "height");
                float radius = Number(element, "rx");
                if (fill != null) canvas.FillRoundedRectangle(x, y, width, height, radius);
                if (stroke != null) canvas.DrawRoundedRectangle(x, y, width, height, radius);
                break;
            case "circle":
                float cx = Number(element, "cx"), cy = Number(element, "cy"), r = Number(element, "r");
                if (fill != null) canvas.FillCircle(cx, cy, r);
                if (stroke != null) canvas.DrawCircle(cx, cy, r);
                break;
            case "ellipse":
                float ex = Number(element, "cx"), ey = Number(element, "cy");
                float rx = Number(element, "rx"), ry = Number(element, "ry");
                if (fill != null) canvas.FillEllipse(ex - rx, ey - ry, rx * 2, ry * 2);
                if (stroke != null) canvas.DrawEllipse(ex - rx, ey - ry, rx * 2, ry * 2);
                break;
            case "line":
                canvas.DrawLine(Number(element, "x1"), Number(element, "y1"), Number(element, "x2"), Number(element, "y2"));
                break;
            case "polyline":
            case "polygon":
                var points = ParsePoints((string?)element.Attribute("points"));
                if (points.Count > 1)
                {
                    var path = new PathF();
                    path.MoveTo(points[0].X, points[0].Y);
                    foreach (var point in points.Skip(1)) path.LineTo(point);
                    if (element.Name.LocalName == "polygon") path.Close();
                    if (fill != null && element.Name.LocalName == "polygon") canvas.FillPath(path);
                    if (stroke != null) canvas.DrawPath(path);
                }
                break;
            case "path":
                var pathData = ParseSimplePath((string?)element.Attribute("d"));
                if (pathData != null)
                {
                    if (fill != null) canvas.FillPath(pathData);
                    if (stroke != null) canvas.DrawPath(pathData);
                }
                break;
        }
    }

    private static List<PointF> ParsePoints(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<PointF>();
        var numbers = Regex.Matches(value, @"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?")
            .Select(match => float.Parse(match.Value, CultureInfo.InvariantCulture)).ToArray();
        var points = new List<PointF>();
        for (int i = 0; i + 1 < numbers.Length; i += 2) points.Add(new PointF(numbers[i], numbers[i + 1]));
        return points;
    }

    private static PathF? ParseSimplePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var tokens = Regex.Matches(value, @"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?")
            .Select(match => match.Value).ToArray();
        var path = new PathF();
        float x = 0, y = 0;
        char command = '\0';
        int index = 0;
        while (index < tokens.Length)
        {
            if (char.IsLetter(tokens[index][0])) command = tokens[index++][0];
            bool relative = char.IsLower(command);
            char upper = char.ToUpperInvariant(command);
            if (upper == 'Z') { path.Close(); command = '\0'; continue; }
            if (upper is not ('M' or 'L' or 'H' or 'V')) return null;
            int needed = upper is 'H' or 'V' ? 1 : 2;
            if (index + needed > tokens.Length) return null;
            float first = float.Parse(tokens[index++], CultureInfo.InvariantCulture);
            float second = upper is 'H' or 'V' ? 0 : float.Parse(tokens[index++], CultureInfo.InvariantCulture);
            float nextX = upper == 'H' ? (relative ? x + first : first) : upper == 'V' ? x : (relative ? x + first : first);
            float nextY = upper == 'V' ? (relative ? y + first : first) : upper == 'H' ? y : (relative ? y + second : second);
            if (upper == 'M') { path.MoveTo(nextX, nextY); command = relative ? 'l' : 'L'; }
            else path.LineTo(nextX, nextY);
            x = nextX;
            y = nextY;
        }
        return path;
    }

    private static float Number(XElement element, string name, float fallback = 0) =>
        float.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static Color ParseColor(string? value, string fallback)
    {
        try { return Color.FromArgb(string.IsNullOrWhiteSpace(value) || value == "none" ? fallback : value); }
        catch { return Color.FromArgb(fallback); }
    }
}
