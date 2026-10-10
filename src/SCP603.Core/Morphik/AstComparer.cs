namespace Scp603.Core.Morphik;

/// <summary>Structural equality for ASTs (records hold arrays, whose default equality is reference based).</summary>
public static class AstComparer
{
    public static bool ProgramEquals(ProgramNode a, ProgramNode b) => StmtListEquals(a.Statements, b.Statements);

    private static bool StmtListEquals(MorphikStmt[] a, MorphikStmt[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (!StmtEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool StmtEquals(MorphikStmt a, MorphikStmt b)
    {
        if (a.GetType() != b.GetType())
        {
            return false;
        }

        return (a, b) switch
        {
            (LetStmt x, LetStmt y) => x.Name == y.Name && ExprEquals(x.Value, y.Value),
            (AssignStmt x, AssignStmt y) => x.Name == y.Name && ExprEquals(x.Value, y.Value),
            (EmitStmt x, EmitStmt y) => NullableExprEquals(x.Value, y.Value),
            (WindowStmt x, WindowStmt y) => NullableExprEquals(x.Value, y.Value),
            (ReturnStmt x, ReturnStmt y) => NullableExprEquals(x.Value, y.Value),
            (BreakStmt, BreakStmt) => true,
            (ExprStmt x, ExprStmt y) => ExprEquals(x.Value, y.Value),
            (IfStmt x, IfStmt y) => ExprEquals(x.Cond, y.Cond)
                && StmtListEquals(x.Then, y.Then)
                && StmtListEquals(x.Else, y.Else),
            (WhileStmt x, WhileStmt y) => ExprEquals(x.Cond, y.Cond) && StmtListEquals(x.Body, y.Body),
            _ => false,
        };
    }

    private static bool NullableExprEquals(MorphikExpr? a, MorphikExpr? b)
        => (a, b) switch
        {
            (null, null) => true,
            (not null, not null) => ExprEquals(a, b),
            _ => false,
        };

    public static bool ExprEquals(MorphikExpr a, MorphikExpr b)
    {
        if (a.GetType() != b.GetType())
        {
            return false;
        }

        return (a, b) switch
        {
            (NumberExpr x, NumberExpr y) => x.Value == y.Value,
            (StringExpr x, StringExpr y) => x.Value == y.Value,
            (IdentExpr x, IdentExpr y) => x.Name == y.Name,
            (CallExpr x, CallExpr y) => x.Name == y.Name && ArgsEqual(x.Args, y.Args),
            (UnaryExpr x, UnaryExpr y) => x.Op == y.Op && ExprEquals(x.Operand, y.Operand),
            (BinaryExpr x, BinaryExpr y) => x.Op == y.Op && ExprEquals(x.Left, y.Left) && ExprEquals(x.Right, y.Right),
            _ => false,
        };
    }

    private static bool ArgsEqual(MorphikExpr[] a, MorphikExpr[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (!ExprEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
