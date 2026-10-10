namespace Scp603.Core.Morphik;

public enum BcOp
{
    Nop = 0,
    PushNum,
    PushStr,
    Load,
    Store,
    Define,
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
    Not,
    Neg,
    Call,
    Emit,
    Window,
    Jump,
    JumpIfFalse,
    Pop,
    Return,
    Break,
    Halt,
}

/// <summary>One VM instruction. <c>A</c> carries an integer operand, <c>S</c> a name/literal operand.</summary>
public readonly record struct Instruction(BcOp Op, long A, string? S = null)
{
    public override string ToString() => S is null ? $"{Op} {A}" : $"{Op} {A} '{S}'";
}

/// <summary>A compiled Morphik program, ready to be persisted as "megaprime".</summary>
public sealed class MorphikBytecode
{
    public MorphikBytecode(Instruction[] instructions)
    {
        Instructions = instructions;
    }

    public Instruction[] Instructions { get; }
}
