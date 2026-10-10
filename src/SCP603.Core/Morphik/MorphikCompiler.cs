namespace Scp603.Core.Morphik;

/// <summary>
/// Lowers a Morphik AST to stack based bytecode. This is the "compiler and linker" role
/// SCP-603 takes on when it is handed a list of files.
/// </summary>
public sealed class MorphikCompiler
{
    private readonly List<Instruction> _code = new();
    private readonly List<(int PatchAt, List<int> BreakJumps)> _loops = new();

    public static MorphikBytecode Compile(ProgramNode program)
    {
        var compiler = new MorphikCompiler();
        compiler.CompileProgram(program);
        compiler.Emit(BcOp.Halt, 0);
        return new MorphikBytecode(compiler._code.ToArray());
    }

    private void CompileProgram(ProgramNode program)
    {
        foreach (MorphikStmt stmt in program.Statements)
        {
            CompileStmt(stmt);
        }
    }

    private void CompileStmt(MorphikStmt stmt)
    {
        switch (stmt)
        {
            case LetStmt l:
                CompileExpr(l.Value);
                Emit(BcOp.Define, 0, l.Name);
                break;

            case AssignStmt a:
                CompileExpr(a.Value);
                Emit(BcOp.Store, 0, a.Name);
                break;

            case EmitStmt e:
                if (e.Value is null)
                {
                    Emit(BcOp.Emit, 0);
                }
                else
                {
                    CompileExpr(e.Value);
                    Emit(BcOp.Emit, 1);
                }

                break;

            case WindowStmt w:
                if (w.Value is null)
                {
                    Emit(BcOp.Window, 0);
                }
                else
                {
                    CompileExpr(w.Value);
                    Emit(BcOp.Window, 1);
                }

                break;

            case ExprStmt x:
                CompileExpr(x.Value);
                Emit(BcOp.Pop, 0);
                break;

            case ReturnStmt r:
                if (r.Value is not null)
                {
                    CompileExpr(r.Value);
                }

                Emit(BcOp.Return, r.Value is null ? 0 : 1);
                break;

            case BreakStmt:
                {
                    int at = Emit(BcOp.Jump, -1);
                    if (_loops.Count > 0)
                    {
                        _loops[^1].BreakJumps.Add(at);
                    }

                    break;
                }

            case IfStmt i:
                CompileExpr(i.Cond);
                int jumpElse = Emit(BcOp.JumpIfFalse, -1);
                foreach (MorphikStmt s in i.Then)
                {
                    CompileStmt(s);
                }

                if (i.Else.Length > 0)
                {
                    int jumpEnd = Emit(BcOp.Jump, -1);
                    Patch(jumpElse, _code.Count);
                    foreach (MorphikStmt s in i.Else)
                    {
                        CompileStmt(s);
                    }

                    Patch(jumpEnd, _code.Count);
                }
                else
                {
                    Patch(jumpElse, _code.Count);
                }

                break;

            case WhileStmt w:
                {
                    int loopStart = _code.Count;
                    CompileExpr(w.Cond);
                    int jumpOut = Emit(BcOp.JumpIfFalse, -1);
                    _loops.Add((loopStart, new List<int>()));
                    foreach (MorphikStmt s in w.Body)
                    {
                        CompileStmt(s);
                    }

                    Emit(BcOp.Jump, loopStart);
                    Patch(jumpOut, _code.Count);
                    foreach (int brk in _loops[^1].BreakJumps)
                    {
                        Patch(brk, _code.Count);
                    }

                    _loops.RemoveAt(_loops.Count - 1);
                    break;
                }

            default:
                throw new InvalidOperationException($"Не удалось скомпилировать {stmt.GetType().Name}");
        }
    }

    private void CompileExpr(MorphikExpr expr)
    {
        switch (expr)
        {
            case NumberExpr n:
                Emit(BcOp.PushNum, n.Value);
                break;

            case StringExpr s:
                Emit(BcOp.PushStr, 0, s.Value);
                break;

            case IdentExpr id:
                Emit(BcOp.Load, 0, id.Name);
                break;

            case UnaryExpr u:
                CompileExpr(u.Operand);
                Emit(u.Op == UnaryOp.Not ? BcOp.Not : BcOp.Neg, 0);
                break;

            case BinaryExpr b:
                CompileExpr(b.Left);
                CompileExpr(b.Right);
                Emit(ToOp(b.Op), 0);
                break;

            case CallExpr c:
                foreach (MorphikExpr arg in c.Args)
                {
                    CompileExpr(arg);
                }

                Emit(BcOp.Call, c.Args.Length, c.Name);
                break;

            default:
                throw new InvalidOperationException($"Не удалось скомпилировать {expr.GetType().Name}");
        }
    }

    private static BcOp ToOp(BinaryOp op) => op switch
    {
        BinaryOp.Add => BcOp.Add,
        BinaryOp.Sub => BcOp.Sub,
        BinaryOp.Mul => BcOp.Mul,
        BinaryOp.Div => BcOp.Div,
        BinaryOp.Mod => BcOp.Mod,
        BinaryOp.Eq => BcOp.Eq,
        BinaryOp.Ne => BcOp.Ne,
        BinaryOp.Lt => BcOp.Lt,
        BinaryOp.Gt => BcOp.Gt,
        BinaryOp.Le => BcOp.Le,
        BinaryOp.Ge => BcOp.Ge,
        BinaryOp.And => BcOp.And,
        BinaryOp.Or => BcOp.Or,
        _ => throw new InvalidOperationException(),
    };

    private int Emit(BcOp op, long a, string? s = null)
    {
        _code.Add(new Instruction(op, a, s));
        return _code.Count - 1;
    }

    private void Patch(int index, int target)
    {
        _code[index] = _code[index] with { A = target };
    }
}
