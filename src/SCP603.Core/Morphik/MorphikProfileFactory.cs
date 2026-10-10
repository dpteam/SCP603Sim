using Scp603.Core.Numerics;

namespace Scp603.Core.Morphik;

/// <summary>
/// Produces the chain of Morphik language generations.
/// Generation v[n+1] is a small, random-but-seeded mutation of v[n], so the language
/// "drifts" gradually. Because every step is seeded only by the version number, replaying
/// the chain always yields exactly the same profiles: the same version behaves the same way.
/// </summary>
public static class MorphikProfileFactory
{
    private static readonly string[] LetPool =
        { "let", "def", "задать", "кв", "put", "✶", "начать", "giv", "низ" };
    private static readonly string[] SetPool =
        { "set", "mov", "присв", "→", "фф", "≔", "putz", "клади", "≡" };
    private static readonly string[] EmitPool =
        { "emit", "say", "печать", "☞", "out", "▷", "печ", "sig", "рцы" };
    private static readonly string[] IfPool =
        { "if", "when", "если", "cond", "когда", "iff", "唯", "раз" };
    private static readonly string[] ElsePool =
        { "else", "иначе", "инак", "otherwise", "sonst", "ино", "仍", "ежли" };
    private static readonly string[] WhilePool =
        { "while", "пока", "loop", "do", "whilst", "цикл", "环", "доколь" };
    private static readonly string[] ReturnPool =
        { "return", "ret", "выход", "give", "назад", "返", "верни" };
    private static readonly string[] BreakPool =
        { "break", "halt", "стоп", "stop", "прервать", "断", "обрыв" };
    private static readonly string[] WindowPool =
        { "window", "окно", "view", "show", "сцена", "窗", "явь" };
    private static readonly string[] TruePool =
        { "true", "да", "истина", "y", "✔", "так" };
    private static readonly string[] FalsePool =
        { "false", "нет", "ложь", "n", "✘", "никак" };
    private static readonly string[] AndPool =
        { "and", "и", "&&", "∧", "also", "аще" };
    private static readonly string[] OrPool =
        { "or", "или", "||", "∨", "either", "либо" };
    private static readonly string[] NotPool =
        { "not", "не", "¬", "нѣ", "нетоже" };

    private static readonly string[] AddPool = { "+", "＋", "⊕", "加", "⋏" };
    private static readonly string[] SubPool = { "-", "−", "⊖", "减", "⋎" };
    private static readonly string[] MulPool = { "*", "×", "·", "⊗", "乘" };
    private static readonly string[] DivPool = { "/", "÷", "∕", "⊘", "除" };
    private static readonly string[] ModPool = { "%", "‰", "⊙", "模", "◦" };
    private static readonly string[] EqPool = { "==", "＝", "⇔", "≡", "равно" };
    private static readonly string[] NePool = { "!=", "≠", "⇎", "неравно" };
    private static readonly string[] LtPool = { "<", "＜", "≺", "мень" };
    private static readonly string[] GtPool = { ">", "＞", "≻", "боль" };
    private static readonly string[] LePool = { "<=", "≤", "≼" };
    private static readonly string[] GePool = { ">=", "≥", "≽" };
    private static readonly string[] AssignPool = { "=", "≔", "←", "↤", "положи" };

    private static readonly string[] BlockOpenPool = { "{", "(", "do", "begin", "⟨", "[" };
    private static readonly string[] BlockClosePool = { "}", ")", "end", "finish", "⟩", "]" };
    private static readonly string[] ParenOpenPool = { "(", "¡", "⟦" };
    private static readonly string[] ParenClosePool = { ")", "!", "⟧" };
    private static readonly string[] ArgSepPool = { ",", "、", ";" };
    private static readonly string[] CommentPool = { "//", "#", "--", "※", ";;" };
    private static readonly string[] TerminatorPool = { ";", ".", "!", "§", "~", "‽" };
    private static readonly int[] BasePool = { 2, 3, 8, 9, 10 };
    private static readonly char[] PrefixPool = { '_', 'λ', '§', '꞉', 'ⰿ', '꙰', 'ᛝ' };
    private static readonly char[] QuotePool = { '"', '\'', '«', '„' };

    public static MorphikProfile CreateInitial()
    {
        var p = new MorphikProfile();
        p.Version = 1;
        return p;
    }

    /// <summary>Builds the profile for the requested version by replaying the deterministic chain.</summary>
    public static MorphikProfile ForVersion(int version)
    {
        if (version < 1)
        {
            version = 1;
        }

        MorphikProfile p = CreateInitial();
        for (int v = 2; v <= version; v++)
        {
            var rng = new XorShift64(unchecked(0x5C603UL ^ ((ulong)v * 0x9E3779B97F4A7C15UL)));
            p = Mutate(p, rng);
        }

        return p;
    }

    public static MorphikProfile Mutate(MorphikProfile prev, XorShift64 rng)
    {
        MorphikProfile p = prev.Clone();
        p.Version = prev.Version + 1;

        if (rng.Chance(0.16)) p.KwLet = PickDistinct(LetPool, p.KwLet, rng);
        if (rng.Chance(0.16)) p.KwSet = PickDistinct(SetPool, p.KwSet, rng);
        if (rng.Chance(0.16)) p.KwEmit = PickDistinct(EmitPool, p.KwEmit, rng);
        if (rng.Chance(0.16)) p.KwIf = PickDistinct(IfPool, p.KwIf, rng);
        if (rng.Chance(0.14)) p.KwElse = PickDistinct(ElsePool, p.KwElse, rng);
        if (rng.Chance(0.16)) p.KwWhile = PickDistinct(WhilePool, p.KwWhile, rng);
        if (rng.Chance(0.10)) p.KwReturn = PickDistinct(ReturnPool, p.KwReturn, rng);
        if (rng.Chance(0.10)) p.KwBreak = PickDistinct(BreakPool, p.KwBreak, rng);
        if (rng.Chance(0.12)) p.KwWindow = PickDistinct(WindowPool, p.KwWindow, rng);
        if (rng.Chance(0.10)) p.KwTrue = PickDistinct(TruePool, p.KwTrue, rng);
        if (rng.Chance(0.10)) p.KwFalse = PickDistinct(FalsePool, p.KwFalse, rng);
        if (rng.Chance(0.10)) p.KwAnd = PickDistinct(AndPool, p.KwAnd, rng);
        if (rng.Chance(0.10)) p.KwOr = PickDistinct(OrPool, p.KwOr, rng);
        if (rng.Chance(0.10)) p.KwNot = PickDistinct(NotPool, p.KwNot, rng);

        if (rng.Chance(0.14)) p.OpAdd = PickDistinct(AddPool, p.OpAdd, rng);
        if (rng.Chance(0.14)) p.OpSub = PickDistinct(SubPool, p.OpSub, rng);
        if (rng.Chance(0.10)) p.OpMul = PickDistinct(MulPool, p.OpMul, rng);
        if (rng.Chance(0.10)) p.OpDiv = PickDistinct(DivPool, p.OpDiv, rng);
        if (rng.Chance(0.10)) p.OpMod = PickDistinct(ModPool, p.OpMod, rng);
        if (rng.Chance(0.12)) p.OpEq = PickDistinct(EqPool, p.OpEq, rng);
        if (rng.Chance(0.12)) p.OpNe = PickDistinct(NePool, p.OpNe, rng);
        if (rng.Chance(0.10)) p.OpLt = PickDistinct(LtPool, p.OpLt, rng);
        if (rng.Chance(0.10)) p.OpGt = PickDistinct(GtPool, p.OpGt, rng);
        if (rng.Chance(0.08)) p.OpLe = PickDistinct(LePool, p.OpLe, rng);
        if (rng.Chance(0.08)) p.OpGe = PickDistinct(GePool, p.OpGe, rng);
        if (rng.Chance(0.12)) p.OpAssign = PickDistinct(AssignPool, p.OpAssign, rng);

        if (rng.Chance(0.10)) p.BlockOpen = PickDistinct(BlockOpenPool, p.BlockOpen, rng);
        if (rng.Chance(0.10)) p.BlockClose = PickDistinct(BlockClosePool, p.BlockClose, rng);
        if (rng.Chance(0.06)) p.ParenOpen = PickDistinct(ParenOpenPool, p.ParenOpen, rng);
        if (rng.Chance(0.06)) p.ParenClose = PickDistinct(ParenClosePool, p.ParenClose, rng);
        if (rng.Chance(0.07)) p.ArgSep = PickDistinct(ArgSepPool, p.ArgSep, rng);
        if (rng.Chance(0.08)) p.CommentPrefix = PickDistinct(CommentPool, p.CommentPrefix, rng);
        if (rng.Chance(0.09)) p.Terminator = PickDistinct(TerminatorPool, p.Terminator, rng);
        if (rng.Chance(0.08)) p.NumericBase = PickDistinctInt(BasePool, p.NumericBase, rng);
        if (rng.Chance(0.05)) p.IdentifierPrefix = PickDistinctChar(PrefixPool, p.IdentifierPrefix, rng);
        if (rng.Chance(0.05)) p.StringQuote = PickDistinctChar(QuotePool, p.StringQuote, rng);

        // Semantics: this is what makes the observable behaviour drift.
        if (rng.Chance(0.12)) p.IndexOneBased = !p.IndexOneBased;
        if (rng.Chance(0.12)) p.Emit = (EmitStyle)rng.NextInt(0, 5);
        if (rng.Chance(0.08)) p.StartValue = rng.Pick(new long[] { 2, 2, 2, 3, 5 });
        if (rng.Chance(0.15)) p.Uses3D = !p.Uses3D;
        if (rng.Chance(0.20)) p.PaletteSeed = (uint)rng.NextInt64(0, 0xFFFFFF);
        if (rng.Chance(0.15)) p.WindowTitle = rng.Pick(new[]
        {
            "SCP-603", "Умереть", "Hello, world!", "Требуется ввод", "ТРИ КРЕСТА",
            "Является ли это", "Сказитель", "Джин, детям грозит беда", "Тамагочи",
        });

        Reconcile(p, rng);
        return p;
    }

    /// <summary>
    /// Guarantees that no two surface tokens share a spelling; otherwise the lexer
    /// would become ambiguous. Called after every mutation.
    /// </summary>
    private static void Reconcile(MorphikProfile p, XorShift64 rng)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);

        // Order matters only for which slot gets re-rolled on a clash.
        (string name, string value, string[] pool, Action<string> set)[] slots =
        {
            ("KwLet", p.KwLet, LetPool, v => p.KwLet = v),
            ("KwSet", p.KwSet, SetPool, v => p.KwSet = v),
            ("KwEmit", p.KwEmit, EmitPool, v => p.KwEmit = v),
            ("KwIf", p.KwIf, IfPool, v => p.KwIf = v),
            ("KwElse", p.KwElse, ElsePool, v => p.KwElse = v),
            ("KwWhile", p.KwWhile, WhilePool, v => p.KwWhile = v),
            ("KwReturn", p.KwReturn, ReturnPool, v => p.KwReturn = v),
            ("KwBreak", p.KwBreak, BreakPool, v => p.KwBreak = v),
            ("KwWindow", p.KwWindow, WindowPool, v => p.KwWindow = v),
            ("KwTrue", p.KwTrue, TruePool, v => p.KwTrue = v),
            ("KwFalse", p.KwFalse, FalsePool, v => p.KwFalse = v),
            ("KwAnd", p.KwAnd, AndPool, v => p.KwAnd = v),
            ("KwOr", p.KwOr, OrPool, v => p.KwOr = v),
            ("KwNot", p.KwNot, NotPool, v => p.KwNot = v),
            ("OpAdd", p.OpAdd, AddPool, v => p.OpAdd = v),
            ("OpSub", p.OpSub, SubPool, v => p.OpSub = v),
            ("OpMul", p.OpMul, MulPool, v => p.OpMul = v),
            ("OpDiv", p.OpDiv, DivPool, v => p.OpDiv = v),
            ("OpMod", p.OpMod, ModPool, v => p.OpMod = v),
            ("OpEq", p.OpEq, EqPool, v => p.OpEq = v),
            ("OpNe", p.OpNe, NePool, v => p.OpNe = v),
            ("OpLt", p.OpLt, LtPool, v => p.OpLt = v),
            ("OpGt", p.OpGt, GtPool, v => p.OpGt = v),
            ("OpLe", p.OpLe, LePool, v => p.OpLe = v),
            ("OpGe", p.OpGe, GePool, v => p.OpGe = v),
            ("OpAssign", p.OpAssign, AssignPool, v => p.OpAssign = v),
            ("BlockOpen", p.BlockOpen, BlockOpenPool, v => p.BlockOpen = v),
            ("BlockClose", p.BlockClose, BlockClosePool, v => p.BlockClose = v),
            ("ParenOpen", p.ParenOpen, ParenOpenPool, v => p.ParenOpen = v),
            ("ParenClose", p.ParenClose, ParenClosePool, v => p.ParenClose = v),
            ("ArgSep", p.ArgSep, ArgSepPool, v => p.ArgSep = v),
            ("CommentPrefix", p.CommentPrefix, CommentPool, v => p.CommentPrefix = v),
            ("Terminator", p.Terminator, TerminatorPool, v => p.Terminator = v),
        };

        foreach (var slot in slots)
        {
            string value = slot.value;
            if (value.Length == 0 || used.Contains(value))
            {
                value = PickDistinct(slot.pool, value, rng, used);
                if (used.Contains(value))
                {
                    // Last resort: keep it unique by tacking on a rarely used glyph.
                    int n = 0;
                    while (used.Contains(value + Suffix(n)))
                    {
                        n++;
                    }

                    value = value + Suffix(n);
                }

                slot.set(value);
            }

            used.Add(value);
        }

        // The identifier prefix must never collide with the leading character of another token.
        while (used.Any(u => u.Length > 0 && u[0] == p.IdentifierPrefix))
        {
            p.IdentifierPrefix = PickDistinctChar(PrefixPool, p.IdentifierPrefix, rng);
        }
    }

    private static string Suffix(int n) => n == 0 ? "‑" : "‑" + n;

    private static string PickDistinct(IReadOnlyList<string> pool, string current, XorShift64 rng)
    {
        for (int attempt = 0; attempt < 32; attempt++)
        {
            string candidate = rng.Pick(pool);
            if (!string.Equals(candidate, current, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return pool[0];
    }

    private static string PickDistinct(IReadOnlyList<string> pool, string current, XorShift64 rng, HashSet<string> used)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            string candidate = rng.Pick(pool);
            if (!string.Equals(candidate, current, StringComparison.Ordinal) && !used.Contains(candidate))
            {
                return candidate;
            }
        }

        return current;
    }

    private static int PickDistinctInt(IReadOnlyList<int> pool, int current, XorShift64 rng)
    {
        int candidate = rng.Pick(pool);
        return candidate == current ? pool[0] : candidate;
    }

    private static char PickDistinctChar(IReadOnlyList<char> pool, char current, XorShift64 rng)
    {
        for (int attempt = 0; attempt < 32; attempt++)
        {
            char candidate = rng.Pick(pool);
            if (candidate != current)
            {
                return candidate;
            }
        }

        return pool[0];
    }
}
