using System.Text;
using Scp603.Core.Numerics;

namespace Scp603.Core.Morphik;

/// <summary>
/// Makes a program look the way the Foundation describes it: "heavily obfuscated code".
/// Renaming is applied consistently and the injected branches are unreachable, so the
/// meaning — and the round trip through the parser — is preserved.
/// </summary>
public static class MorphikObfuscator
{
    public static ProgramNode Obfuscate(ProgramNode program, XorShift64 rng)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var collected = new HashSet<string>(StringComparer.Ordinal);
        CollectIdentifiers(program, collected);
        var used = new HashSet<string>(StringComparer.Ordinal) { ProgramTemplate.FnIsPrime, ProgramTemplate.FnNextPrime };
        foreach (string original in collected)
        {
            string candidate;
            do
            {
                candidate = RandomName(rng);
            }
            while (used.Contains(candidate));

            used.Add(candidate);
            names[original] = candidate;
        }

        var renamed = new ProgramNode(RewriteList(program.Statements, names));

        // Sprinkle a few unreachable branches over the top level.
        int deadCount = rng.NextInt(1, 4);
        var top = new List<MorphikStmt>(renamed.Statements);
        for (int k = 0; k < deadCount; k++)
        {
            int pos = rng.NextInt(0, top.Count + 1);
            top.Insert(pos, DeadBranch(rng));
        }

        return new ProgramNode(top.ToArray());
    }

    private static void CollectIdentifiers(ProgramNode program, ICollection<string> into)
    {
        void WalkStmt(MorphikStmt s)
        {
            switch (s)
            {
                case LetStmt l:
                    into.Add(l.Name);
                    WalkExpr(l.Value);
                    break;
                case AssignStmt a:
                    into.Add(a.Name);
                    WalkExpr(a.Value);
                    break;
                case EmitStmt e when e.Value is not null:
                    WalkExpr(e.Value);
                    break;
                case WindowStmt w when w.Value is not null:
                    WalkExpr(w.Value);
                    break;
                case ReturnStmt r when r.Value is not null:
                    WalkExpr(r.Value);
                    break;
                case ExprStmt x:
                    WalkExpr(x.Value);
                    break;
                case IfStmt i:
                    WalkExpr(i.Cond);
                    foreach (MorphikStmt t in i.Then)
                    {
                        WalkStmt(t);
                    }

                    foreach (MorphikStmt t in i.Else)
                    {
                        WalkStmt(t);
                    }

                    break;
                case WhileStmt w:
                    WalkExpr(w.Cond);
                    foreach (MorphikStmt t in w.Body)
                    {
                        WalkStmt(t);
                    }

                    break;
            }
        }

        void WalkExpr(MorphikExpr e)
        {
            switch (e)
            {
                case IdentExpr id:
                    into.Add(id.Name);
                    break;
                case CallExpr c:
                    foreach (MorphikExpr arg in c.Args)
                    {
                        WalkExpr(arg);
                    }

                    break;
                case UnaryExpr u:
                    WalkExpr(u.Operand);
                    break;
                case BinaryExpr b:
                    WalkExpr(b.Left);
                    WalkExpr(b.Right);
                    break;
            }
        }

        foreach (MorphikStmt s in program.Statements)
        {
            WalkStmt(s);
        }
    }

    private static MorphikStmt[] RewriteList(MorphikStmt[] statements, Dictionary<string, string> names)
        => statements.Select(s => Rewrite(s, names)).ToArray();

    private static MorphikStmt Rewrite(MorphikStmt s, Dictionary<string, string> names) => s switch
    {
        LetStmt l => new LetStmt(Rename(l.Name, names), RewriteExpr(l.Value, names)),
        AssignStmt a => new AssignStmt(Rename(a.Name, names), RewriteExpr(a.Value, names)),
        EmitStmt e => new EmitStmt(e.Value is null ? null : RewriteExpr(e.Value, names)),
        WindowStmt w => new WindowStmt(w.Value is null ? null : RewriteExpr(w.Value, names)),
        ReturnStmt r => new ReturnStmt(r.Value is null ? null : RewriteExpr(r.Value, names)),
        BreakStmt b => b,
        ExprStmt x => new ExprStmt(RewriteExpr(x.Value, names)),
        IfStmt i => new IfStmt(
            RewriteExpr(i.Cond, names),
            RewriteList(i.Then, names),
            RewriteList(i.Else, names)),
        WhileStmt w => new WhileStmt(RewriteExpr(w.Cond, names), RewriteList(w.Body, names)),
        _ => s,
    };

    private static MorphikExpr RewriteExpr(MorphikExpr e, Dictionary<string, string> names) => e switch
    {
        IdentExpr id => new IdentExpr(Rename(id.Name, names)),
        CallExpr c => new CallExpr(c.Name, c.Args.Select(a => RewriteExpr(a, names)).ToArray()),
        UnaryExpr u => new UnaryExpr(u.Op, RewriteExpr(u.Operand, names)),
        BinaryExpr b => new BinaryExpr(b.Op, RewriteExpr(b.Left, names), RewriteExpr(b.Right, names)),
        _ => e,
    };

    private static string Rename(string name, Dictionary<string, string> names)
        => names.TryGetValue(name, out string? mapped) ? mapped : name;

    private static MorphikStmt DeadBranch(XorShift64 rng)
    {
        MorphikExpr cond = new BinaryExpr(
            BinaryOp.Eq,
            new NumberExpr(rng.NextInt(0, 2)),
            new NumberExpr(rng.NextInt(2, 5))); // always false

        return new IfStmt(
            cond,
            new MorphikStmt[] { new EmitStmt(new NumberExpr(rng.NextInt(0, 1000))) },
            Array.Empty<MorphikStmt>());
    }

    private static string RandomName(XorShift64 rng)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz";
        int len = rng.NextInt(3, 7);
        var sb = new StringBuilder();
        sb.Append(alphabet[rng.NextInt(0, alphabet.Length)]);
        for (int i = 1; i < len; i++)
        {
            char c = rng.NextBool()
                ? alphabet[rng.NextInt(0, alphabet.Length)]
                : (char)('0' + rng.NextInt(0, 10));
            sb.Append(c);
        }

        return sb.ToString();
    }
}
