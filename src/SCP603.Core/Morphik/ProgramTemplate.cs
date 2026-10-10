namespace Scp603.Core.Morphik;

/// <summary>
/// The canonical "main thread" behaviour of SCP-603 rendered as a Morphik program:
/// generate primes and, whenever the index of a prime is itself prime, request a window.
/// The profile decides where counting starts (0- or 1-based) and which value to begin with,
/// which is exactly why different versions open windows on different numbers.
/// </summary>
public static class ProgramTemplate
{
    public const string VarN = "n";
    public const string VarI = "i";
    public const string FnIsPrime = "isprime";
    public const string FnNextPrime = "nextprime";

    public static ProgramNode Build(MorphikProfile profile)
    {
        MorphikExpr one = new NumberExpr(1);

        MorphikStmt emitPrime = new EmitStmt(Ident(VarN));

        MorphikStmt windowWhenIndexPrime = new IfStmt(
            Call(FnIsPrime, Ident(VarI)),
            new MorphikStmt[] { new WindowStmt(Ident(VarN)) },
            Array.Empty<MorphikStmt>());

        MorphikStmt advanceIndex = new AssignStmt(VarI, Add(Ident(VarI), one));

        MorphikStmt mainBranch = new IfStmt(
            Call(FnIsPrime, Ident(VarN)),
            new MorphikStmt[] { emitPrime, windowWhenIndexPrime, advanceIndex },
            Array.Empty<MorphikStmt>());

        MorphikStmt advanceCandidate = new AssignStmt(VarN, Add(Ident(VarN), one));

        MorphikStmt loop = new WhileStmt(
            one,
            new MorphikStmt[] { mainBranch, advanceCandidate });

        return new ProgramNode(new MorphikStmt[]
        {
            new LetStmt(VarN, new NumberExpr(profile.StartValue)),
            new LetStmt(VarI, new NumberExpr(profile.IndexOneBased ? 1 : 0)),
            loop,
        });
    }

    private static MorphikExpr Ident(string name) => new IdentExpr(name);

    private static MorphikExpr Add(MorphikExpr a, MorphikExpr b)
        => new BinaryExpr(BinaryOp.Add, a, b);

    private static MorphikExpr Call(string name, params MorphikExpr[] args)
        => new CallExpr(name, args);
}
