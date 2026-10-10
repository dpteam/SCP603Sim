using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Scp603.Core.Rendering;

namespace Scp603.Gui;

/// <summary>
/// Adapts the scene drawing interface onto a WPF <see cref="DrawingContext"/>, so the exact
/// same scene code that the tests drive headlessly is what appears on screen.
/// </summary>
public sealed class WpfCanvas : ICanvas
{
    private static readonly Typeface Face =
        new(new FontFamily("Consolas, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static readonly Typeface BoldFace =
        new(new FontFamily("Consolas, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private readonly DrawingContext _dc;

    public WpfCanvas(DrawingContext dc, int width, int height)
    {
        _dc = dc;
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public void Clear(uint argb) => _dc.DrawRectangle(BrushOf(argb), null, new Rect(0, 0, Width, Height));

    public void FillRect(int x, int y, int w, int h, uint argb)
    {
        if (w <= 0 || h <= 0)
        {
            return;
        }

        _dc.DrawRectangle(BrushOf(argb), null, new Rect(x, y, w, h));
    }

    public void DrawRect(int x, int y, int w, int h, uint argb)
    {
        if (w <= 0 || h <= 0)
        {
            return;
        }

        _dc.DrawRectangle(null, PenOf(argb, 1), new Rect(x + 0.5, y + 0.5, w - 1, h - 1));
    }

    public void DrawLine(int x0, int y0, int x1, int y1, uint argb)
        => _dc.DrawLine(PenOf(argb, 1), new Point(x0 + 0.5, y0 + 0.5), new Point(x1 + 0.5, y1 + 0.5));

    public void FillCircle(int cx, int cy, int radius, uint argb)
    {
        if (radius <= 0)
        {
            return;
        }

        _dc.DrawEllipse(BrushOf(argb), null, new Point(cx, cy), radius, radius);
    }

    public void DrawTriangle(int x0, int y0, int x1, int y1, int x2, int y2, uint argb, bool filled)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(x0, y0), filled, true);
            ctx.LineTo(new Point(x1, y1), true, false);
            ctx.LineTo(new Point(x2, y2), true, false);
        }

        geometry.Freeze();
        _dc.DrawGeometry(filled ? BrushOf(argb) : null, filled ? null : PenOf(argb, 1), geometry);
    }

    public void FillPolygon(ReadOnlySpan<(int X, int Y)> points, uint argb)
    {
        if (points.Length < 3)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), true, true);
            for (int i = 1; i < points.Length; i++)
            {
                ctx.LineTo(new Point(points[i].X, points[i].Y), true, false);
            }
        }

        geometry.Freeze();
        _dc.DrawGeometry(BrushOf(argb), null, geometry);
    }

    public void DrawText(int x, int y, string text, uint argb, int size = 12, bool bold = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        FormattedText ft = MakeText(text, argb, size, bold);
        _dc.DrawText(ft, new Point(x, y));
    }

    public void MeasureText(string text, int size, bool bold, out int width, out int height)
    {
        if (string.IsNullOrEmpty(text))
        {
            width = 0;
            height = Math.Max(12, (int)(size * 1.3));
            return;
        }

        FormattedText ft = MakeText(text, 0xFFFFFFFF, size, bold);
        width = (int)Math.Ceiling(ft.Width);
        height = (int)Math.Ceiling(ft.Height);
    }

    private static FormattedText MakeText(string text, uint argb, int size, bool bold)
        => new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            bold ? BoldFace : Face,
            size,
            BrushOf(argb),
            1.0);

    private static readonly Dictionary<uint, SolidColorBrush> BrushCache = new();

    private static SolidColorBrush BrushOf(uint argb)
    {
        lock (BrushCache)
        {
            if (BrushCache.TryGetValue(argb, out SolidColorBrush? cached))
            {
                return cached;
            }

            var brush = new SolidColorBrush(Color.FromArgb(
                (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
            brush.Freeze();
            BrushCache[argb] = brush;
            return brush;
        }
    }

    private static readonly Dictionary<uint, Pen> PenCache = new();

    private static Pen PenOf(uint argb, double thickness)
    {
        uint key = argb ^ ((uint)thickness << 28);
        lock (PenCache)
        {
            if (PenCache.TryGetValue(key, out Pen? cached))
            {
                return cached;
            }

            var pen = new Pen(BrushOf(argb), thickness);
            pen.Freeze();
            PenCache[key] = pen;
            return pen;
        }
    }
}
