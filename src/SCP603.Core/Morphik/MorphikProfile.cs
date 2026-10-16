using System.Globalization;
using System.Text;

namespace Scp603.Core.Morphik;

/// <summary>
/// A full description of one generation of the "Morphik" language:
/// keywords, operators, delimiters, numeric base, comments and — importantly — semantics.
/// Because it is stored inside the program itself, a version can always re-interpret the
/// source it produced. There is deliberately no backwards compatibility.
/// </summary>
public sealed class MorphikProfile
{
    public int Version { get; set; } = 1;
    public string LanguageName { get; set; } = "Морфик";

    // --- lexical surface -------------------------------------------------
    public int NumericBase { get; set; } = 10;
    public char IdentifierPrefix { get; set; } = '_';
    public char StringQuote { get; set; } = '"';
    public string CommentPrefix { get; set; } = "//";
    public string Terminator { get; set; } = "\n"; // "\n" or a symbol such as ";" / "." / "!"
    public bool NewlineTerminates => Terminator == "\n";

    // keyword spellings
    public string KwLet { get; set; } = "let";
    public string KwSet { get; set; } = "set";
    public string KwEmit { get; set; } = "emit";
    public string KwIf { get; set; } = "if";
    public string KwElse { get; set; } = "else";
    public string KwWhile { get; set; } = "while";
    public string KwReturn { get; set; } = "return";
    public string KwBreak { get; set; } = "break";
    public string KwWindow { get; set; } = "window";
    public string KwTrue { get; set; } = "true";
    public string KwFalse { get; set; } = "false";
    public string KwAnd { get; set; } = "and";
    public string KwOr { get; set; } = "or";
    public string KwNot { get; set; } = "not";

    // operators
    public string OpAssign { get; set; } = "=";
    public string OpAdd { get; set; } = "+";
    public string OpSub { get; set; } = "-";
    public string OpMul { get; set; } = "*";
    public string OpDiv { get; set; } = "/";
    public string OpMod { get; set; } = "%";
    public string OpEq { get; set; } = "==";
    public string OpNe { get; set; } = "!=";
    public string OpLt { get; set; } = "<";
    public string OpGt { get; set; } = ">";
    public string OpLe { get; set; } = "<=";
    public string OpGe { get; set; } = ">=";

    // delimiters
    public string BlockOpen { get; set; } = "{";
    public string BlockClose { get; set; } = "}";
    public string ParenOpen { get; set; } = "(";
    public string ParenClose { get; set; } = ")";
    public string ArgSep { get; set; } = ",";

    // --- semantics (these are what make an observing researchers' day interesting) ---
    public bool IndexOneBased { get; set; } = true;
    public EmitStyle Emit { get; set; } = EmitStyle.Decimal;
    public long StartValue { get; set; } = 2;

    // --- interface / presentation -------------------------------------------------
    public string WindowTitle { get; set; } = "SCP-603";
    public uint PaletteSeed { get; set; } = 0x603;
    public bool Uses3D { get; set; }

    /// <summary>Digest of the previous source genome from which this version was mutated.</summary>
    public ulong ParentDigest { get; set; }

    /// <summary>Human-readable mutation record embedded in the generated source header.</summary>
    public string MutationSummary { get; set; } = "initial genome";

    /// <summary>Seed used for deterministic code/icon mutation.</summary>
    public ulong GenomeSeed { get; set; }

    public MorphikProfile Clone() => (MorphikProfile)MemberwiseClone();

    /// <summary>Stable 64-bit digest of the profile, used to tag compiled artefacts.</summary>
    public ulong Digest()
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;

        void Feed(string? s)
        {
            foreach (char c in s ?? string.Empty)
            {
                hash ^= c;
                hash *= prime;
            }

            hash ^= 0x1F;
            hash *= prime;
        }

        Feed(LanguageName);
        Feed(NumericBase.ToString(CultureInfo.InvariantCulture));
        Feed(IdentifierPrefix.ToString());
        Feed(StringQuote.ToString());
        Feed(CommentPrefix);
        Feed(Terminator);
        Feed(KwLet); Feed(KwSet); Feed(KwEmit); Feed(KwIf); Feed(KwElse);
        Feed(KwWhile); Feed(KwReturn); Feed(KwBreak); Feed(KwWindow);
        Feed(KwTrue); Feed(KwFalse); Feed(KwAnd); Feed(KwOr); Feed(KwNot);
        Feed(OpAssign); Feed(OpAdd); Feed(OpSub); Feed(OpMul); Feed(OpDiv); Feed(OpMod);
        Feed(OpEq); Feed(OpNe); Feed(OpLt); Feed(OpGt); Feed(OpLe); Feed(OpGe);
        Feed(BlockOpen); Feed(BlockClose); Feed(ParenOpen); Feed(ParenClose); Feed(ArgSep);
        Feed(IndexOneBased ? "1" : "0");
        Feed(((int)Emit).ToString(CultureInfo.InvariantCulture));
        Feed(StartValue.ToString(CultureInfo.InvariantCulture));
        Feed(Uses3D ? "3d" : "2d");
        Feed(PaletteSeed.ToString(CultureInfo.InvariantCulture));
        Feed(ParentDigest.ToString(CultureInfo.InvariantCulture));
        Feed(MutationSummary);
        Feed(GenomeSeed.ToString(CultureInfo.InvariantCulture));
        return hash;
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append("Морфик v").Append(Version)
          .Append("  base=").Append(NumericBase)
          .Append("  id-prefix='").Append(IdentifierPrefix).Append('\'')
          .Append("  term=").Append(NewlineTerminates ? "NL" : Terminator)
          .Append("  if=").Append(KwIf)
          .Append("  while=").Append(KwWhile)
          .Append("  emit=").Append(KwEmit)
           .Append("  index=").Append(IndexOneBased ? "1-based" : "0-based")
           .Append("  emitStyle=").Append(Emit)
           .Append("  genome=").Append(Digest().ToString("x16", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
