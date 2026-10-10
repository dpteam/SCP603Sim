using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Scp603.IconGen;

/// <summary>
/// Generates the SCP-603 icon entirely with the .NET base class library + GDI+.
/// The motif is Яков (SCP-603-20): a red/blue/green equilateral triangle on black.
/// </summary>
public static class Program
{
    private static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    public static int Main(string[] args)
    {
        string outputDir = args.Length > 0 ? args[0] : "assets";
        Directory.CreateDirectory(outputDir);
        string icoPath = Path.Combine(outputDir, "scp603.ico");
        string pngPath = Path.Combine(outputDir, "scp603.png");

        var pngs = new List<(int Size, byte[] Png)>();
        foreach (int size in Sizes)
        {
            pngs.Add((size, RenderPng(size)));
        }

        WriteIco(icoPath, pngs);
        File.WriteAllBytes(pngPath, pngs.First(p => p.Size == 256).Png);
        Console.WriteLine($"Значок записан: {Path.GetFullPath(icoPath)}");
        return 0;
    }

    private static byte[] RenderPng(int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Black);

            // Subtle border so the icon reads on light backgrounds too.
            using (var pen = new Pen(Color.FromArgb(70, 90, 90, 90), Math.Max(1f, size / 32f)))
            {
                g.DrawRectangle(pen, 0, 0, size - 1, size - 1);
            }

            float margin = size * 0.18f;
            float side = size - (2 * margin);
            float height = side * 0.866f; // equilateral

            var top = new PointF(size / 2f, (size - height) / 2f);
            var right = new PointF(size / 2f + (side / 2f), (size - height) / 2f + height);
            var left = new PointF(size / 2f - (side / 2f), (size - height) / 2f + height);
            var center = new PointF((top.X + right.X + left.X) / 3f, (top.Y + right.Y + left.Y) / 3f);

            DrawGradientTriangle(g, top, right, center, Color.Red, Color.Blue);
            DrawGradientTriangle(g, right, left, center, Color.Blue, Color.Lime);
            DrawGradientTriangle(g, left, top, center, Color.Lime, Color.Red);

            // Vertex nodes.
            float node = Math.Max(1.2f, size * 0.06f);
            using var red = new SolidBrush(Color.Red);
            using var blue = new SolidBrush(Color.Blue);
            using var green = new SolidBrush(Color.Lime);
            FillNode(g, red, top, node);
            FillNode(g, blue, right, node);
            FillNode(g, green, left, node);

            if (size >= 48)
            {
                using var font = new Font("Consolas", size * 0.20f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
                var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("603", font, brush, new RectangleF(0, size * 0.62f, size, size * 0.3f), fmt);
            }
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static void DrawGradientTriangle(Graphics g, PointF a, PointF b, PointF c, Color from, Color to)
    {
        var pts = new[] { a, b, c };
        using var brush = new LinearGradientBrush(a, b, from, to);
        g.FillPolygon(brush, pts);
    }

    private static void FillNode(Graphics g, Brush brush, PointF p, float r)
        => g.FillEllipse(brush, p.X - r, p.Y - r, r * 2, r * 2);

    private static void WriteIco(string path, List<(int Size, byte[] Png)> images)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(fs);

        w.Write((short)0); // reserved
        w.Write((short)1); // type = icon
        w.Write((short)images.Count);

        int offset = 6 + (images.Count * 16);
        foreach ((int size, byte[] png) in images)
        {
            w.Write((byte)(size >= 256 ? 0 : size)); // width (0 => 256)
            w.Write((byte)(size >= 256 ? 0 : size)); // height
            w.Write((byte)0); // colours in palette
            w.Write((byte)0); // reserved
            w.Write((short)1);  // colour planes
            w.Write((short)32); // bits per pixel
            w.Write(png.Length);
            w.Write(offset);
            offset += png.Length;
        }

        foreach ((_, byte[] png) in images)
        {
            w.Write(png);
        }
    }
}
