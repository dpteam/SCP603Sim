namespace Scp603.Core.Morphik;

/// <summary>
/// Logical token categories. The concrete spelling of each keyword/operator is supplied
/// by the version specific <see cref="MorphikProfile"/>, so the surface of the language
/// drifts from version to version while the grammar underneath stays stable.
/// </summary>
public enum TokenKind
{
    Eof = 0,
    NewLine,
    Comment,
    Term,

    // Keywords
    KwLet,
    KwSet,
    KwEmit,
    KwIf,
    KwElse,
    KwWhile,
    KwReturn,
    KwBreak,
    KwWindow,
    KwTrue,
    KwFalse,
    KwAnd,
    KwOr,
    KwNot,

    // Literals / names
    Ident,
    Number,
    String,

    // Operators
    OpAssign,
    OpAdd,
    OpSub,
    OpMul,
    OpDiv,
    OpMod,
    OpEq,
    OpNe,
    OpLt,
    OpGt,
    OpLe,
    OpGe,

    // Delimiters
    LParen,
    RParen,
    LBrace,
    RBrace,
    Comma,
}
