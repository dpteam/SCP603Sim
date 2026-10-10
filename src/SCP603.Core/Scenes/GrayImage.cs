using Scp603.Core.Numerics;

namespace Scp603.Core.Scenes;

/// <summary>
/// A tiny 8-bit grayscale image plus decoders for the dependency-free formats
/// PGM (Netpbm, P2/P5) and uncompressed BMP. Anything more exotic is generated procedurally,
/// so the simulator needs no third-party imaging library.
/// </summary>
public sealed class GrayImage
{
    public GrayImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public byte At(int x, int y) => Pixels[(y * Width) + x];

    public double AspectRatio => Height == 0 ? 1 : (double)Width / Height;

    /// <summary>A stable content hash, used to pick a deterministic "story".</summary>
    public ulong ContentHash()
    {
        ulong h = 1469598103934665603UL;
        foreach (byte b in Pixels)
        {
            h ^= b;
            h *= 1099511628211UL;
        }

        return h;
    }

    public static bool IsSupported(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".pgm" or ".pbm" or ".ppm" or ".bmp";
    }

    public static GrayImage? TryLoad(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".pgm" => LoadPgm(File.ReadAllBytes(path)),
                ".ppm" or ".pbm" => LoadPgm(File.ReadAllBytes(path)),
                ".bmp" => LoadBmp(File.ReadAllBytes(path)),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Deterministic procedural image, so the scene always has something to chew on.</summary>
    public static GrayImage Procedural(int width, int height, ulong seed)
    {
        var rng = new XorShift64(seed);
        var px = new byte[width * height];
        // Sum of a few smooth components => looks like a photograph-ish gradient.
        double f1 = 0.5 + rng.NextDouble() * 3;
        double f2 = 0.5 + rng.NextDouble() * 3;
        double f3 = 0.5 + rng.NextDouble() * 6;
        double p1 = rng.NextDouble() * Math.PI * 2;
        double p2 = rng.NextDouble() * Math.PI * 2;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double u = (double)x / width;
                double v = (double)y / height;
                double value =
                    (Math.Sin((u * f1 * 6) + p1) + 1) * 0.25 +
                    (Math.Cos((v * f2 * 5) + p2) + 1) * 0.25 +
                    (Math.Sin(((u + v) * f3 * 3)) + 1) * 0.25 +
                    0.125;
                value = Math.Clamp(value, 0, 1);
                px[(y * width) + x] = (byte)(value * 255);
            }
        }

        return new GrayImage(width, height, px);
    }

    private static GrayImage? LoadPgm(byte[] data)
    {
        // Very small Netpbm reader: handles 'P5' (binary) and 'P2' (ascii), 8-bit only.
        int i = 0;
        string NextToken()
        {
            while (i < data.Length && char.IsWhiteSpace((char)data[i]))
            {
                i++;
            }

            if (i < data.Length && data[i] == '#')
            {
                while (i < data.Length && data[i] != '\n')
                {
                    i++;
                }

                return NextToken();
            }

            int start = i;
            while (i < data.Length && !char.IsWhiteSpace((char)data[i]))
            {
                i++;
            }

            return System.Text.Encoding.ASCII.GetString(data, start, i - start);
        }

        string magic = NextToken();
        if (magic is not ("P5" or "P2"))
        {
            return null;
        }

        int width = int.Parse(NextToken());
        int height = int.Parse(NextToken());
        int maxVal = int.Parse(NextToken());
        if (maxVal <= 0 || maxVal > 255 || width <= 0 || height <= 0)
        {
            return null;
        }

        var px = new byte[width * height];
        if (magic == "P5")
        {
            i++; // single whitespace before the binary payload
            for (int k = 0; k < px.Length && i < data.Length; k++)
            {
                px[k] = (byte)(data[i++] * 255 / maxVal);
            }
        }
        else
        {
            for (int k = 0; k < px.Length; k++)
            {
                px[k] = (byte)(int.Parse(NextToken()) * 255 / maxVal);
            }
        }

        return new GrayImage(width, height, px);
    }

    private static GrayImage? LoadBmp(byte[] data)
    {
        if (data.Length < 54 || data[0] != 'B' || data[1] != 'M')
        {
            return null;
        }

        int dataOffset = BitConverter.ToInt32(data, 10);
        int width = BitConverter.ToInt32(data, 18);
        int height = BitConverter.ToInt32(data, 22);
        short bpp = BitConverter.ToInt16(data, 28);
        if (bpp != 24 || width <= 0)
        {
            return null;
        }

        bool topDown = height < 0;
        int h = Math.Abs(height);
        int rowSize = ((width * 3) + 3) & ~3;
        var px = new byte[width * h];
        for (int y = 0; y < h; y++)
        {
            int srcY = topDown ? y : (h - 1 - y);
            int rowStart = dataOffset + (srcY * rowSize);
            for (int x = 0; x < width; x++)
            {
                int p = rowStart + (x * 3);
                if (p + 2 >= data.Length)
                {
                    return null;
                }

                int b = data[p], g = data[p + 1], r = data[p + 2];
                px[(y * width) + x] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
            }
        }

        return new GrayImage(width, h, px);
    }
}
