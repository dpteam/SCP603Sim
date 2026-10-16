using System.Globalization;
using System.Text;
using Scp603.Core.Numerics;

namespace Scp603.Core.Morphik;

/// <summary>
/// Archive builder for the self-copy thread. It emits three deterministic artefacts per
/// version: Morphik source (owned by the kernel), a valid C# companion source file, and a
/// small palette/icon image. The C# companion is source-only: the simulator never invokes
/// a compiler or loads generated assemblies.
/// </summary>
public static class MorphikSourceForge
{
    public static IReadOnlyList<string> DescribeMutations(MorphikProfile parent, MorphikProfile child)
    {
        var changes = new List<string>();

        AddIfChanged(changes, "let keyword", parent.KwLet, child.KwLet);
        AddIfChanged(changes, "set keyword", parent.KwSet, child.KwSet);
        AddIfChanged(changes, "emit keyword", parent.KwEmit, child.KwEmit);
        AddIfChanged(changes, "if keyword", parent.KwIf, child.KwIf);
        AddIfChanged(changes, "while keyword", parent.KwWhile, child.KwWhile);
        AddIfChanged(changes, "window keyword", parent.KwWindow, child.KwWindow);
        AddIfChanged(changes, "assignment operator", parent.OpAssign, child.OpAssign);
        AddIfChanged(changes, "addition operator", parent.OpAdd, child.OpAdd);
        AddIfChanged(changes, "comparison operator", parent.OpEq, child.OpEq);
        AddIfChanged(changes, "block delimiter", parent.BlockOpen + parent.BlockClose, child.BlockOpen + child.BlockClose);
        AddIfChanged(changes, "comment syntax", parent.CommentPrefix, child.CommentPrefix);
        AddIfChanged(changes, "statement terminator", parent.Terminator, child.Terminator);

        if (parent.NumericBase != child.NumericBase) changes.Add($"numeric base {parent.NumericBase}->{child.NumericBase}");
        if (parent.IdentifierPrefix != child.IdentifierPrefix) changes.Add("identifier prefix");
        if (parent.StringQuote != child.StringQuote) changes.Add("string quotation");
        if (parent.IndexOneBased != child.IndexOneBased) changes.Add("index origin");
        if (parent.StartValue != child.StartValue) changes.Add($"entry point {parent.StartValue}->{child.StartValue}");
        if (parent.Emit != child.Emit) changes.Add($"output style {parent.Emit}->{child.Emit}");
        if (parent.Uses3D != child.Uses3D) changes.Add($"renderer {(child.Uses3D ? "2D->3D" : "3D->2D")}");
        if (parent.PaletteSeed != child.PaletteSeed) changes.Add("palette seed");
        if (parent.WindowTitle != child.WindowTitle) changes.Add("window title");

        if (changes.Count == 0)
        {
            changes.Add("obfuscation layout and internal seed");
        }

        return changes;
    }

    public static string GenerateCSharpCompanion(
        MorphikProfile parent,
        MorphikProfile child,
        string morphikSource,
        IReadOnlyList<string> mutations)
    {
        string digest = child.Digest().ToString("x16", CultureInfo.InvariantCulture);
        string parentDigest = parent.Digest().ToString("x16", CultureInfo.InvariantCulture);
        string sourceBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(morphikSource));
        string mutationArray = string.Join(", ", mutations.Select(m => "\"" + Escape(m) + "\""));
        string ns = "Scp603.Generated.V" + child.Version.ToString(CultureInfo.InvariantCulture);

        // This is intentionally ordinary, reviewable C# rather than an opaque binary blob.
        // A researcher can inspect or compile it manually; the simulator itself never does.
        return $"// SCP-603 / MORPHIK SELF-COPY / VERSION {child.Version}\n" +
               $"// parent={parentDigest}; digest={digest}\n" +
               $"// generated from a deterministic genome seed 0x{child.GenomeSeed:x16}\n" +
               "#nullable enable\n" +
               "using System;\n" +
               "using System.Text;\n\n" +
               $"namespace {ns};\n\n" +
               "/// <summary>Generated companion source archived by SCP-603.</summary>\n" +
               "internal static class SelfCopy\n" +
               "{\n" +
               $"    internal const int Version = {child.Version};\n" +
               $"    internal const ulong ParentDigest = 0x{parentDigest}UL;\n" +
               $"    internal const ulong Digest = 0x{digest}UL;\n" +
               $"    internal const ulong GenomeSeed = 0x{child.GenomeSeed:x16}UL;\n" +
               $"    internal const uint PaletteSeed = 0x{child.PaletteSeed:x8}U;\n" +
               $"    internal const string WindowTitle = \"{Escape(child.WindowTitle)}\";\n" +
               $"    internal static readonly string[] Mutations = new[] {{ {mutationArray} }};\n\n" +
               $"    internal static string MorphikSource() => Encoding.UTF8.GetString(Convert.FromBase64String(\"{sourceBase64}\"));\n" +
               "}\n";
    }

    public static string WriteCompanion(
        string directory,
        MorphikProfile parent,
        MorphikProfile child,
        string morphikSource,
        out string mutationSummary,
        out string iconPath)
    {
        Directory.CreateDirectory(directory);
        IReadOnlyList<string> mutations = DescribeMutations(parent, child);
        mutationSummary = string.Join("; ", mutations);
        child.MutationSummary = mutationSummary;

        string companionPath = Path.Combine(directory, $"v{child.Version:D4}.SelfCopy.g.cs");
        File.WriteAllText(
            companionPath,
            GenerateCSharpCompanion(parent, child, morphikSource, mutations),
            new UTF8Encoding(false));

        iconPath = WriteVersionIco(directory, child);
        return companionPath;
    }

    private static string WriteVersionIco(string directory, MorphikProfile profile)
    {
        const int size = 64;
        string path = Path.Combine(directory, $"v{profile.Version:D4}.ico");
        int red = (int)((profile.PaletteSeed >> 16) & 0xFF);
        int green = (int)((profile.PaletteSeed >> 8) & 0xFF);
        int blue = (int)(profile.PaletteSeed & 0xFF);
        var rng = new XorShift64(profile.PaletteSeed ^ profile.Digest());

        // One-image ICO containing an uncompressed 32-bit DIB. This avoids GDI+/NuGet in Core.
        const int dibHeaderSize = 40;
        const int xorBytes = size * size * 4;
        const int andBytes = size * ((size + 31) / 32 * 4);
        const int imageBytes = dibHeaderSize + xorBytes + andBytes;
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write((byte)size);
        writer.Write((byte)size);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(imageBytes);
        writer.Write(6 + 16);

        writer.Write(dibHeaderSize);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(xorBytes);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        // DIB pixels are bottom-up and stored as BGRA.
        for (int y = size - 1; y >= 0; y--)
        {
            for (int x = 0; x < size; x++)
            {
                bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                bool triangle = IsInsideTriangle(x, y, size);
                int noise = rng.NextInt(-12, 13);
                int r = border ? 32 : triangle ? Clamp(red + 70 + noise) : 0;
                int g = border ? 38 : triangle ? Clamp(green + 30 + noise) : 0;
                int b = border ? 44 : triangle ? Clamp(blue + 90 + noise) : 0;
                writer.Write((byte)b);
                writer.Write((byte)g);
                writer.Write((byte)r);
                writer.Write((byte)255);
            }
        }

        // Fully transparent AND mask: alpha in the 32-bit pixels controls visibility.
        for (int i = 0; i < andBytes; i++)
        {
            writer.Write((byte)0);
        }

        return path;
    }

    private static void AddIfChanged(List<string> list, string label, string before, string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            list.Add($"{label} '{before}'->'{after}'");
        }
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 255);

    private static bool IsInsideTriangle(int x, int y, int size)
    {
        double center = size / 2.0;
        double top = size * 0.12;
        double bottom = size * 0.88;
        double halfWidth = (y - top) * 0.577;
        return y >= top && y <= bottom && x >= center - halfWidth && x <= center + halfWidth;
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
