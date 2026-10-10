using System.Text;

namespace Scp603.Core.Morphik;

/// <summary>
/// Renders an AST back into Morphik source using a concrete version's surface syntax.
/// Output is fully parenthesised, so parsing it back yields an identical tree.
/// </summary>
public sealed class CodeGenerator
{
    private readonly MorphikProfile _profile;

    public CodeGenerator(MorphikProfile profile)
    {
        _profile = profile;
    }

    public static string Generate(MorphikProfile profile, ProgramNode program)
        => new CodeGenerator(profile).Render(program);

    public string Render(ProgramNode program)
    {
        var sb = new StringBuilder();
        foreach (MorphikStmt stmt in program.Statements)
        {
            EmitStmt(sb, stmt, 0);
        }

        return sb.ToString();
    }

    private void Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 4);

    private void EndLine(StringBuilder sb)
    {
        if (!_profile.NewlineTerminates)
        {
            sb.Append(_profile.Terminator);
        }

        sb.Append('\n');
    }

    private void EmitStmt(StringBuilder sb, MorphikStmt stmt, int depth)
    {
        switch (stmt)
        {
            case LetStmt let:
                Indent(sb, depth);
                sb.Append(_profile.KwLet).Append(' ').Append(Ident(let.Name))
                  .Append(' ').Append(_profile.OpAssign).Append(' ')
                  .Append(Expr(let.Value));
                EndLine(sb);
                break;

            case AssignStmt assign:
                Indent(sb, depth);
                sb.Append(Ident(assign.Name)).Append(' ').Append(_profile.OpAssign).Append(' ')
                  .Append(Expr(assign.Value));
                EndLine(sb);
                break;

            case EmitStmt emit:
                Indent(sb, depth);
                sb.Append(_profile.KwEmit);
                if (emit.Value is not null)
                {
                    sb.Append(' ').Append(Expr(emit.Value));
                }

                EndLine(sb);
                break;

            case WindowStmt window:
                Indent(sb, depth);
                sb.Append(_profile.KwWindow);
                if (window.Value is not null)
                {
                    sb.Append(' ').Append(Expr(window.Value));
                }

                EndLine(sb);
                break;

            case ReturnStmt ret:
                Indent(sb, depth);
                sb.Append(_profile.KwReturn);
                if (ret.Value is not null)
                {
                    sb.Append(' ').Append(Expr(ret.Value));
                }

                EndLine(sb);
                break;

            case BreakStmt:
                Indent(sb, depth);
                sb.Append(_profile.KwBreak);
                EndLine(sb);
                break;

            case ExprStmt exprStmt:
                Indent(sb, depth);
                sb.Append(Expr(exprStmt.Value));
                EndLine(sb);
                break;

            case IfStmt ifStmt:
                Indent(sb, depth);
                sb.Append(_profile.KwIf).Append(' ').Append(Expr(ifStmt.Cond)).Append(' ')
                  .Append(_profile.BlockOpen).Append('\n');
                foreach (MorphikStmt inner in ifStmt.Then)
                {
                    EmitStmt(sb, inner, depth + 1);
                }

                if (ifStmt.Else.Length > 0)
                {
                    Indent(sb, depth);
                    sb.Append(_profile.BlockClose).Append(' ').Append(_profile.KwElse).Append(' ')
                      .Append(_profile.BlockOpen).Append('\n');
                    foreach (MorphikStmt inner in ifStmt.Else)
                    {
                        EmitStmt(sb, inner, depth + 1);
                    }

                    Indent(sb, depth);
                    sb.Append(_profile.BlockClose);
                    EndLine(sb);
                }
                else
                {
                    Indent(sb, depth);
                    sb.Append(_profile.BlockClose);
                    EndLine(sb);
                }

                break;

            case WhileStmt whileStmt:
                Indent(sb, depth);
                sb.Append(_profile.KwWhile).Append(' ').Append(Expr(whileStmt.Cond)).Append(' ')
                  .Append(_profile.BlockOpen).Append('\n');
                foreach (MorphikStmt inner in whileStmt.Body)
                {
                    EmitStmt(sb, inner, depth + 1);
                }

                Indent(sb, depth);
                sb.Append(_profile.BlockClose);
                EndLine(sb);
                break;

            default:
                throw new InvalidOperationException($"Неизвестный оператор: {stmt.GetType().Name}");
        }
    }

    private string Ident(string name) => _profile.IdentifierPrefix + name;

    private string Expr(MorphikExpr expr) => expr switch
    {
        NumberExpr n => Number(n.Value),
        StringExpr s => String(s.Value),
        IdentExpr id => Ident(id.Name),
        CallExpr call => Call(call),
        UnaryExpr u => Unary(u),
        BinaryExpr b => Binary(b),
        _ => throw new InvalidOperationException($"Неизвестное выражение: {expr.GetType().Name}"),
    };

    private string Call(CallExpr call)
    {
        var sb = new StringBuilder();
        sb.Append(Ident(call.Name)).Append(_profile.ParenOpen);
        for (int i = 0; i < call.Args.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(' ').Append(_profile.ArgSep).Append(' ');
            }

            sb.Append(Expr(call.Args[i]));
        }

        sb.Append(_profile.ParenClose);
        return sb.ToString();
    }

    private string Unary(UnaryExpr u)
    {
        string op = u.Op == UnaryOp.Not ? _profile.KwNot : _profile.OpSub;
        return $"{op} {Expr(u.Operand)}";
    }

    private string Binary(BinaryExpr b)
    {
        string op = b.Op switch
        {
            BinaryOp.Add => _profile.OpAdd,
            BinaryOp.Sub => _profile.OpSub,
            BinaryOp.Mul => _profile.OpMul,
            BinaryOp.Div => _profile.OpDiv,
            BinaryOp.Mod => _profile.OpMod,
            BinaryOp.Eq => _profile.OpEq,
            BinaryOp.Ne => _profile.OpNe,
            BinaryOp.Lt => _profile.OpLt,
            BinaryOp.Gt => _profile.OpGt,
            BinaryOp.Le => _profile.OpLe,
            BinaryOp.Ge => _profile.OpGe,
            BinaryOp.And => _profile.KwAnd,
            BinaryOp.Or => _profile.KwOr,
            _ => throw new InvalidOperationException(),
        };

        return _profile.ParenOpen + Expr(b.Left) + " " + op + " " + Expr(b.Right) + _profile.ParenClose;
    }

    private string Number(long value)
    {
        if (value == 0)
        {
            return "0";
        }

        int b = _profile.NumericBase;
        if (b is < 2 or > 10)
        {
            b = 10;
        }

        var sb = new StringBuilder();
        long v = value;
        bool negative = v < 0;
        if (negative)
        {
            v = -v;
        }

        while (v > 0)
        {
            sb.Insert(0, (char)('0' + (int)(v % b)));
            v /= b;
        }

        if (negative)
        {
            sb.Insert(0, '-');
        }

        return sb.ToString();
    }

    private string String(string value)
    {
        var sb = new StringBuilder();
        sb.Append(_profile.StringQuote);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c == _profile.StringQuote)
                    {
                        sb.Append('\\').Append(c);
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append(_profile.StringQuote);
        return sb.ToString();
    }
}
