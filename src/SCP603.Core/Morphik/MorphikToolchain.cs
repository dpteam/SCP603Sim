using System.Globalization;

namespace Scp603.Core.Morphik;

/// <summary>
/// The "compiler and linker" facade: text in, megaprime artefact out, and back again.
/// </summary>
public static class MorphikToolchain
{
    public static ProgramNode Parse(MorphikProfile profile, string source)
        => Parser.Parse(profile, source);

    public static string Generate(MorphikProfile profile, ProgramNode program)
        => CodeGenerator.Generate(profile, program);

    public static MorphikBytecode Compile(ProgramNode program)
        => MorphikCompiler.Compile(program);

    public static MegaprimeArtifact BuildArtifact(MorphikProfile profile, ProgramNode program)
        => new()
        {
            ProfileVersion = profile.Version,
            ProfileDigest = profile.Digest(),
            Source = CodeGenerator.Generate(profile, program),
            Bytecode = MorphikCompiler.Compile(program),
        };

    /// <summary>Round trips a Morphik source string through parser + code generator.</summary>
    public static bool RoundTrips(MorphikProfile profile, string source)
    {
        ProgramNode first = Parser.Parse(profile, source);
        string regenerated = CodeGenerator.Generate(profile, first);
        ProgramNode second = Parser.Parse(profile, regenerated);
        return AstComparer.ProgramEquals(first, second);
    }

    /// <summary>Renders a numeric value the way the current Morphik version would print it.</summary>
    public static string FormatValue(object? value, MorphikProfile profile)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string s)
        {
            return s;
        }

        long number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        int b = profile.NumericBase is < 2 or > 10 ? 10 : profile.NumericBase;
        return profile.Emit switch
        {
            EmitStyle.Hex => ToBase(number, 16),
            EmitStyle.Binary => ToBase(number, 2),
            EmitStyle.Grouped => number.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " "),
            EmitStyle.Words => SpellOut(number),
            _ => ToBase(number, b),
        };
    }

    private static string ToBase(long value, int b)
    {
        if (value == 0)
        {
            return "0";
        }

        bool negative = value < 0;
        long v = Math.Abs(value);
        var sb = new System.Text.StringBuilder();
        while (v > 0)
        {
            int digit = (int)(v % b);
            sb.Insert(0, digit < 10 ? (char)('0' + digit) : (char)('a' + (digit - 10)));
            v /= b;
        }

        if (negative)
        {
            sb.Insert(0, '-');
        }

        return sb.ToString();
    }

    private static string SpellOut(long value)
    {
        // A deliberately simple "verbal" style, deterministic and side effect free.
        if (value is >= 0 and <= 20)
        {
            return new[]
            {
                "ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь",
                "восемь", "девять", "десять", "одиннадцать", "двенадцать", "тринадцать",
                "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать",
                "восемнадцать", "девятнадцать", "двадцать",
            }[value];
        }

        return "число-" + value.ToString(CultureInfo.InvariantCulture);
    }
}
