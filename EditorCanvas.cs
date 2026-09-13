using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NanoUintEditor;

/// <summary>Rendering layer: Static (redrawn only on change) vs Dynamic (redrawn every frame).</summary>
public enum RenderLayer { Static, Dynamic }

/// <summary>Lightweight canvas using DrawingVisual for high-performance batch rendering.
/// Replaces the one-element-one-Control approach. Static layer redraws on data change;
/// Dynamic layer redraws every frame (selection handles, marquee, guidelines).</summary>
public sealed class EditorCanvas
{
    private readonly DrawingVisual _staticVisual = new();
    private readonly DrawingVisual _dynamicVisual = new();
    private readonly DrawingVisual _gridVisual = new();

    /// <summary>Host these DrawingVisuals in a WPF panel's visual tree.</summary>
    public DrawingVisual GridVisual => _gridVisual;
    public DrawingVisual StaticVisual => _staticVisual;
    public DrawingVisual DynamicVisual => _dynamicVisual;

    private double _zoom = 1.0;
    private Vector _pan;
    private Size _canvasSize = new(1920, 1080);
    private bool _staticDirty = true;
    private bool _gridDirty = true;

    // Grid settings
    public bool ShowGrid { get; set; } = true;
    public double GridSpacing { get; set; } = 50;
    public Color GridColor { get; set; } = Colors.LightGray;

    // Canvas dimensions (logical units)
    public Size CanvasSize
    {
        get => _canvasSize;
        set { _canvasSize = value; _staticDirty = true; _gridDirty = true; }
    }

    public double Zoom
    {
        get => _zoom;
        set { _zoom = Math.Max(0.1, Math.Min(5.0, value)); _staticDirty = true; _gridDirty = true; }
    }

    public Vector Pan
    {
        get => _pan;
        set { _pan = value; _staticDirty = true; _gridDirty = true; }
    }

    public bool IsStaticDirty => _staticDirty;

    public EditorCanvas()
    {
    }

    /// <summary>Maps screen coordinates to canvas coordinates.</summary>
    public Point ScreenToCanvas(Point screen)
    {
        return new Point(
            (screen.X - _pan.X) / _zoom,
            (screen.Y - _pan.Y) / _zoom);
    }

    /// <summary>Maps canvas coordinates to screen coordinates.</summary>
    public Point CanvasToScreen(Point canvas)
    {
        return new Point(
            canvas.X * _zoom + _pan.X,
            canvas.Y * _zoom + _pan.Y);
    }

    /// <summary>Converts canvas units to screen pixels.</summary>
    public double ScaleToScreen(double canvasUnits) => canvasUnits * _zoom;

    /// <summary>Converts screen pixels to canvas units.</summary>
    public double ScaleToCanvas(double screenPixels) => screenPixels / _zoom;

    #region Static Layer

    /// <summary>Draws all page elements onto the static layer.</summary>
    public void DrawElements(SlidePage page, RenderingCache? cache = null)
    {
        var dc = _staticVisual.RenderOpen();
        try
        {
            // Draw background
            if (!string.IsNullOrEmpty(page.BackgroundPath) && cache != null
                && cache.TryGetImage(page.BackgroundPath, out var bgImage))
            {
                dc.DrawImage(bgImage, new Rect(0, 0, _canvasSize.Width, _canvasSize.Height));
            }

            // Draw elements sorted by ZIndex
            foreach (var elem in page.Elements.OrderBy(e => e.ZIndex))
            {
                if (!elem.Visible) continue;
                DrawElement(dc, elem, cache);
            }
        }
        finally
        {
            dc.Close();
        }
        _staticDirty = false;
    }

    private void DrawElement(DrawingContext dc, SlideElement elem, RenderingCache? cache)
    {
        var rect = new Rect(elem.X, elem.Y, elem.Width, elem.Height);

        // Background brush
        Brush? brush = null;
        if (!string.IsNullOrEmpty(elem.Props.GetValueOrDefault("spritePath") as string)
            && cache != null
            && cache.TryGetImage(elem.Props["spritePath"]!.ToString()!, out var img))
        {
            brush = new ImageBrush(img);
        }

        if (brush == null)
        {
            // Fallback: colored rectangle based on type
            var color = elem.Type switch
            {
                "Text" => Colors.LightBlue,
                "DialogueBox" => Colors.Beige,
                "ChoiceGroup" => Colors.LightGreen,
                "Audio" => Colors.LightYellow,
                "Flash" => Colors.LightCoral,
                "AdvanceIndicator" => Colors.LightGray,
                _ => Colors.LightSteelBlue,
            };
            brush = new SolidColorBrush(color);
        }

        if (brush != null)
        {
            if (elem.Opacity < 1.0)
            {
                brush = brush.Clone();
                brush.Opacity = elem.Opacity;
            }
            dc.DrawRectangle(brush, null, rect);
        }

        // Draw text content
        if (elem.Type == "Text")
        {
            var content = elem.Props.GetValueOrDefault("content")?.ToString() ?? "";
            if (!string.IsNullOrEmpty(content))
            {
                var fontSize = elem.Props.GetValueOrDefault("fontSize") as double? ?? 24;
                var textColor = ParseColor(elem.Props.GetValueOrDefault("textColor")?.ToString());
                var ft = new FormattedText(content,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    fontSize,
                    new SolidColorBrush(textColor),
                    VisualTreeHelper.GetDpi(_staticVisual).PixelsPerDip);
                dc.DrawText(ft, new Point(elem.X + 5, elem.Y + 5));
            }
        }
    }

    /// <summary>Marks the static layer as needing redraw.</summary>
    public void InvalidateStatic() => _staticDirty = true;

    #endregion

    #region Dynamic Layer

    /// <summary>Draws selection handles, marquee, and guidelines on the dynamic layer.
    /// Called every frame during interaction.</summary>
    public void DrawDynamic(
        IReadOnlyList<SlideElement> selectedElements,
        Rect? marqueeRect,
        IReadOnlyList<(Point start, Point end)>? guidelines = null)
    {
        var dc = _dynamicVisual.RenderOpen();
        try
        {
            // Draw guidelines
            if (guidelines != null)
            {
                var guidePen = new Pen(new SolidColorBrush(Colors.Cyan), 1.0 / _zoom);
                guidePen.DashStyle = DashStyles.Dash;
                foreach (var (start, end) in guidelines)
                {
                    dc.DrawLine(guidePen, start, end);
                }
            }

            // Draw selection rectangle for each selected element
            var selPen = new Pen(new SolidColorBrush(Colors.DodgerBlue), 2.0 / _zoom);
            foreach (var elem in selectedElements)
            {
                var rect = new Rect(elem.X, elem.Y, elem.Width, elem.Height);
                dc.DrawRectangle(null, selPen, rect);

                // Draw resize handles
                DrawResizeHandles(dc, rect, selPen);
            }

            // Draw marquee selection
            if (marqueeRect.HasValue)
            {
                var marqueePen = new Pen(new SolidColorBrush(Colors.DodgerBlue), 1.0 / _zoom);
                marqueePen.DashStyle = DashStyles.Dash;
                dc.DrawRectangle(
                    new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)),
                    marqueePen,
                    marqueeRect.Value);
            }
        }
        finally
        {
            dc.Close();
        }
    }

    private void DrawResizeHandles(DrawingContext dc, Rect rect, Pen pen)
    {
        double hs = HandleSize / _zoom;
        var brush = new SolidColorBrush(Colors.White);
        var handles = new[]
        {
            new Rect(rect.Left - hs / 2, rect.Top - hs / 2, hs, hs),       // TopLeft
            new Rect(rect.Right - hs / 2, rect.Top - hs / 2, hs, hs),      // TopRight
            new Rect(rect.Left - hs / 2, rect.Bottom - hs / 2, hs, hs),    // BottomLeft
            new Rect(rect.Right - hs / 2, rect.Bottom - hs / 2, hs, hs),   // BottomRight
            new Rect(rect.Left + rect.Width / 2 - hs / 2, rect.Top - hs / 2, hs, hs),    // TopCenter
            new Rect(rect.Left + rect.Width / 2 - hs / 2, rect.Bottom - hs / 2, hs, hs), // BottomCenter
            new Rect(rect.Left - hs / 2, rect.Top + rect.Height / 2 - hs / 2, hs, hs),   // MiddleLeft
            new Rect(rect.Right - hs / 2, rect.Top + rect.Height / 2 - hs / 2, hs, hs),  // MiddleRight
        };

        foreach (var handle in handles)
            dc.DrawRectangle(brush, pen, handle);
    }

    private const double HandleSize = 8;

    /// <summary>Hit-tests which resize handle (if any) is at the given canvas point.</summary>
    public string? HitTestHandle(Point canvasPoint, SlideElement elem)
    {
        double hs = HandleSize / _zoom / 2;
        var rect = new Rect(elem.X, elem.Y, elem.Width, elem.Height);

        var handles = new Dictionary<string, Rect>
        {
            ["tl"] = new Rect(rect.Left - hs, rect.Top - hs, hs * 2, hs * 2),
            ["tr"] = new Rect(rect.Right - hs, rect.Top - hs, hs * 2, hs * 2),
            ["bl"] = new Rect(rect.Left - hs, rect.Bottom - hs, hs * 2, hs * 2),
            ["br"] = new Rect(rect.Right - hs, rect.Bottom - hs, hs * 2, hs * 2),
            ["tc"] = new Rect(rect.Left + rect.Width / 2 - hs, rect.Top - hs, hs * 2, hs * 2),
            ["bc"] = new Rect(rect.Left + rect.Width / 2 - hs, rect.Bottom - hs, hs * 2, hs * 2),
            ["ml"] = new Rect(rect.Left - hs, rect.Top + rect.Height / 2 - hs, hs * 2, hs * 2),
            ["mr"] = new Rect(rect.Right - hs, rect.Top + rect.Height / 2 - hs, hs * 2, hs * 2),
        };

        foreach (var (name, r) in handles)
            if (r.Contains(canvasPoint)) return name;
        return null;
    }

    /// <summary>Hit-tests which element is at the given canvas point (topmost first).</summary>
    public SlideElement? HitTestElement(Point canvasPoint, IReadOnlyList<SlideElement> elements)
    {
        foreach (var elem in elements.OrderByDescending(e => e.ZIndex))
        {
            if (!elem.Visible) continue;
            var rect = new Rect(elem.X, elem.Y, elem.Width, elem.Height);
            if (rect.Contains(canvasPoint)) return elem;
        }
        return null;
    }

    #endregion

    #region Grid Layer

    public void DrawGrid()
    {
        if (!_gridDirty) return;
        if (!ShowGrid) { _gridDirty = false; return; }

        var dc = _gridVisual.RenderOpen();
        try
        {
            var pen = new Pen(new SolidColorBrush(GridColor), 0.5 / _zoom);
            pen.Freeze();

            double spacing = GridSpacing;
            for (double x = 0; x <= _canvasSize.Width; x += spacing)
                dc.DrawLine(pen, new Point(x, 0), new Point(x, _canvasSize.Height));
            for (double y = 0; y <= _canvasSize.Height; y += spacing)
                dc.DrawLine(pen, new Point(0, y), new Point(_canvasSize.Width, y));
        }
        finally
        {
            dc.Close();
        }
        _gridDirty = false;
    }

    public void InvalidateGrid() => _gridDirty = true;

    #endregion

    #region Snapping

    /// <summary>Computes snap candidates for the given element against all other elements.</summary>
    public SnapResult ComputeSnap(
        SlideElement moving,
        IReadOnlyList<SlideElement> others,
        double threshold = 5.0)
    {
        var result = new SnapResult();
        var rect = new Rect(moving.X, moving.Y, moving.Width, moving.Height);

        foreach (var other in others)
        {
            if (other.Id == moving.Id) continue;
            var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);

            // Horizontal snapping (X axis)
            SnapAxis(rect.Left, otherRect.Left, threshold, result.LeftSnaps);
            SnapAxis(rect.Left, otherRect.Right, threshold, result.LeftSnaps);
            SnapAxis(rect.Right, otherRect.Left, threshold, result.RightSnaps);
            SnapAxis(rect.Right, otherRect.Right, threshold, result.RightSnaps);
            SnapAxis(rect.Left + rect.Width / 2, otherRect.Left + otherRect.Width / 2, threshold, result.CenterXSnaps);

            // Vertical snapping (Y axis)
            SnapAxis(rect.Top, otherRect.Top, threshold, result.TopSnaps);
            SnapAxis(rect.Top, otherRect.Bottom, threshold, result.TopSnaps);
            SnapAxis(rect.Bottom, otherRect.Top, threshold, result.BottomSnaps);
            SnapAxis(rect.Bottom, otherRect.Bottom, threshold, result.BottomSnaps);
            SnapAxis(rect.Top + rect.Height / 2, otherRect.Top + otherRect.Height / 2, threshold, result.CenterYSnaps);
        }

        return result;
    }

    private static void SnapAxis(double pos, double candidate, double threshold, List<double> snaps)
    {
        if (Math.Abs(pos - candidate) <= threshold)
            snaps.Add(candidate);
    }

    #endregion

    #region Helpers

    private static Color ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return Colors.Black;
        try
        {
            if (hex.StartsWith('#'))
                return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch { }
        return Colors.Black;
    }

    #endregion
}

/// <summary>Result of snap computation: candidate positions for each edge.</summary>
public sealed class SnapResult
{
    public List<double> LeftSnaps { get; } = new();
    public List<double> RightSnaps { get; } = new();
    public List<double> TopSnaps { get; } = new();
    public List<double> BottomSnaps { get; } = new();
    public List<double> CenterXSnaps { get; } = new();
    public List<double> CenterYSnaps { get; } = new();

    public bool HasSnaps =>
        LeftSnaps.Count > 0 || RightSnaps.Count > 0 ||
        TopSnaps.Count > 0 || BottomSnaps.Count > 0 ||
        CenterXSnaps.Count > 0 || CenterYSnaps.Count > 0;
}

/// <summary>Cache for rendered images (sprites, backgrounds).</summary>
public sealed class RenderingCache
{
    private readonly Dictionary<string, BitmapSource> _cache = new();

    public void Add(string key, BitmapSource image) => _cache[key] = image;
    public bool TryGetImage(string key, out BitmapSource image) => _cache.TryGetValue(key, out image!);
    public void Clear() => _cache.Clear();
    public int Count => _cache.Count;
}
