namespace Scp603.Core.Morphik;

public enum BinaryOp
{
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    Eq,
    Ne,
    Lt,
    Gt,
    Le,
    Ge,
    And,
    Or,
}

public enum UnaryOp
{
    Not,
    Neg,
}

public abstract record MorphikNode;

public abstract record MorphikExpr : MorphikNode;

public sealed record NumberExpr(long Value) : MorphikExpr;

public sealed record StringExpr(string Value) : MorphikExpr;

/// <summary>Identifier without the version-specific prefix character.</summary>
public sealed record IdentExpr(string Name) : MorphikExpr;

public sealed record CallExpr(string Name, MorphikExpr[] Args) : MorphikExpr;

public sealed record BinaryExpr(BinaryOp Op, MorphikExpr Left, MorphikExpr Right) : MorphikExpr;

public sealed record UnaryExpr(UnaryOp Op, MorphikExpr Operand) : MorphikExpr;

public abstract record MorphikStmt : MorphikNode;

public sealed record LetStmt(string Name, MorphikExpr Value) : MorphikStmt;

public sealed record AssignStmt(string Name, MorphikExpr Value) : MorphikStmt;

public sealed record EmitStmt(MorphikExpr? Value) : MorphikStmt;

public sealed record WindowStmt(MorphikExpr? Value) : MorphikStmt;

public sealed record IfStmt(MorphikExpr Cond, MorphikStmt[] Then, MorphikStmt[] Else) : MorphikStmt;

public sealed record WhileStmt(MorphikExpr Cond, MorphikStmt[] Body) : MorphikStmt;

public sealed record ReturnStmt(MorphikExpr? Value) : MorphikStmt;

public sealed record BreakStmt : MorphikStmt;

public sealed record ExprStmt(MorphikExpr Value) : MorphikStmt;

public sealed record ProgramNode(MorphikStmt[] Statements) : MorphikNode;
