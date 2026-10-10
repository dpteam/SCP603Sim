using System.Text;

namespace Scp603.Core.Rendering;

/// <summary>
/// Headless canvas used for tests, logs and the CLI. Draws into a character grid sized in
/// pixels (one cell = <see cref="CellWidth"/> x <see cref="CellHeight"/> pixels).
/// </summary>
public sealed class TextCanvas : ICanvas
{
    private readonly char[] _cells;

    public TextCanvas(int width, int height, int cellWidth = 8, int cellHeight = 16, char background = ' ')
    {
        CellWidth = Math.Max(1, cellWidth);
        CellHeight = Math.Max(1, cellHeight);
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Cols = Math.Max(1, Width / CellWidth);
        Rows = Math.Max(1, Height / CellHeight);
        _cells = new char[Cols * Rows];
        Array.Fill(_cells, background);
    }

    public int Width { get; }

    public int Height { get; }

    public int CellWidth { get; }

    public int CellHeight { get; }

    public int Cols { get; }

    public int Rows { get; }

    private int Index(int col, int row) => (row * Cols) + col;

    private void Put(int col, int row, char c)
    {
        if (col < 0 || row < 0 || col >= Cols || row >= Rows)
        {
            return;
        }

        _cells[Index(col, row)] = c;
    }

    private char Get(int col, int row)
    {
        if (col < 0 || row < 0 || col >= Cols || row >= Rows)
        {
            return ' ';
        }

        return _cells[Index(col, row)];
    }

    public void Clear(uint argb) => Array.Fill(_cells, ' ');

    public void FillRect(int x, int y, int w, int h, uint argb)
    {
        if (w <= 0 || h <= 0)
        {
            return;
        }

        int c0 = x / CellWidth;
        int r0 = y / CellHeight;
        int c1 = (x + w - 1) / CellWidth;
        int r1 = (y + h - 1) / CellHeight;
        for (int r = r0; r <= r1; r++)
        {
            for (int c = c0; c <= c1; c++)
            {
                Put(c, r, '█');
            }
        }
    }

    public void DrawRect(int x, int y, int w, int h, uint argb)
    {
        FillRect(x, y, w, 1, argb);
        FillRect(x, y + h - 1, w, 1, argb);
        FillRect(x, y, 1, h, argb);
        FillRect(x + w - 1, y, 1, h, argb);
    }

    public void DrawLine(int x0, int y0, int x1, int y1, uint argb)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int px = x0, py = y0;
        int guard = 0;
        while (guard++ < 100_000)
        {
            Put(px / CellWidth, py / CellHeight, '·');
            if (px == x1 && py == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                px += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                py += sy;
            }
        }
    }

    public void FillCircle(int cx, int cy, int radius, uint argb)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) <= r2)
                {
                    Put((cx + x) / CellWidth, (cy + y) / CellHeight, '●');
                }
            }
        }
    }

    public void DrawTriangle(int x0, int y0, int x1, int y1, int x2, int y2, uint argb, bool filled)
    {
        if (!filled)
        {
            DrawLine(x0, y0, x1, y1, argb);
            DrawLine(x1, y1, x2, y2, argb);
            DrawLine(x2, y2, x0, y0, argb);
            return;
        }

        FillPolygon(new[] { (x0, y0), (x1, y1), (x2, y2) }, argb);
    }

    public void FillPolygon(ReadOnlySpan<(int X, int Y)> points, uint argb)
    {
        if (points.Length < 3)
        {
            return;
        }

        int minY = int.MaxValue, maxY = int.MinValue;
        foreach ((int _, int y) in points)
        {
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        var nodes = new List<int>();
        for (int y = minY; y <= maxY; y += Math.Max(1, CellHeight))
        {
            nodes.Clear();
            for (int i = 0; i < points.Length; i++)
            {
                (int x1, int y1) = points[i];
                (int x2, int y2) = points[(i + 1) % points.Length];
                if ((y1 < y && y2 >= y) || (y2 < y && y1 >= y))
                {
                    int x = x1 + (int)((double)(y - y1) / (y2 - y1) * (x2 - x1));
                    nodes.Add(x);
                }
            }

            nodes.Sort();
            for (int i = 0; i + 1 < nodes.Count; i += 2)
            {
                int cx0 = nodes[i] / CellWidth;
                int cx1 = nodes[i + 1] / CellWidth;
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    Put(cx, y / CellHeight, '▓');
                }
            }
        }
    }

    public void DrawText(int x, int y, string text, uint argb, int size = 12, bool bold = false)
    {
        int col = x / CellWidth;
        int row = y / CellHeight;
        foreach (char c in text)
        {
            Put(col, row, c);
            col++;
        }
    }

    public void MeasureText(string text, int size, bool bold, out int width, out int height)
    {
        // One glyph per cell keeps wrapping consistent with what Render() shows.
        width = text.Length * CellWidth;
        height = CellHeight;
    }

    public string Render()
    {
        var sb = new StringBuilder((Cols + 1) * Rows);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                sb.Append(Get(c, r));
            }

            if (r < Rows - 1)
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }
}
