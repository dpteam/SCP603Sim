namespace Scp603.Core.Rendering;

/// <summary>
/// Minimal drawing surface shared by the real (GDI+) windows and the headless text renderer.
/// Scene code targets this abstraction only, which keeps the whole logic testable.
/// </summary>
public interface ICanvas
{
    int Width { get; }

    int Height { get; }

    void Clear(uint argb);

    void FillRect(int x, int y, int w, int h, uint argb);

    void DrawRect(int x, int y, int w, int h, uint argb);

    void DrawLine(int x0, int y0, int x1, int y1, uint argb);

    void FillCircle(int cx, int cy, int radius, uint argb);

    void DrawTriangle(int x0, int y0, int x1, int y1, int x2, int y2, uint argb, bool filled);

    void FillPolygon(ReadOnlySpan<(int X, int Y)> points, uint argb);

    void DrawText(int x, int y, string text, uint argb, int size = 12, bool bold = false);

    void MeasureText(string text, int size, bool bold, out int width, out int height);
}

/// <summary>Colour helpers (0xAARRGGBB).</summary>
public static class Rgba
{
    public const uint Black = 0xFF000000;
    public const uint White = 0xFFFFFFFF;
    public const uint Red = 0xFFFF0000;
    public const uint Green = 0xFF00FF00;
    public const uint Blue = 0xFF0000FF;

    public static uint FromArgb(int a, int r, int g, int b)
        => (uint)(((a & 0xFF) << 24) | ((r & 0xFF) << 16) | ((g & 0xFF) << 8) | (b & 0xFF));

    public static uint Gray(int level) => FromArgb(255, level, level, level);

    public static uint Lerp(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        int ar = (int)((a >> 16) & 0xFF), ag = (int)((a >> 8) & 0xFF), ab = (int)(a & 0xFF);
        int br = (int)((b >> 16) & 0xFF), bg = (int)((b >> 8) & 0xFF), bb = (int)(b & 0xFF);
        return FromArgb(255,
            (int)(ar + (br - ar) * t),
            (int)(ag + (bg - ag) * t),
            (int)(ab + (bb - ab) * t));
    }
}
