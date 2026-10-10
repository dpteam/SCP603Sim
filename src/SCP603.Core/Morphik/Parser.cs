namespace Scp603.Core.Morphik;

/// <summary>
/// Recursive descent parser. The grammar is stable; only the surface spellings come from
/// <see cref="MorphikProfile"/>, which is why a version can always re-read its own output.
/// </summary>
public sealed class Parser
{
    private readonly MorphikProfile _profile;
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(MorphikProfile profile, List<Token> tokens)
    {
        _profile = profile;
        _tokens = tokens;
    }

    public static ProgramNode Parse(MorphikProfile profile, string source)
    {
        var tokens = new Lexer(profile).Lex(source);
        return new Parser(profile, tokens).ParseProgram();
    }

    private Token Current => _tokens[_pos];

    private Token Peek(int offset = 1)
    {
        int idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx] : _tokens[^1];
    }

    private Token Advance() => _tokens[_pos++];

    private bool Match(TokenKind kind)
    {
        if (Current.Kind == kind)
        {
            _pos++;
            return true;
        }

        return false;
    }

    private Token Expect(TokenKind kind, string what)
    {
        if (Current.Kind != kind)
        {
            throw new MorphikSyntaxException(
                $"Ожидалось {what}, найдено {Current}", Current.Line, Current.Column);
        }

        return Advance();
    }

    private void SkipNewLines()
    {
        while (Current.Kind == TokenKind.NewLine)
        {
            _pos++;
        }
    }

    public ProgramNode ParseProgram()
    {
        var statements = new List<MorphikStmt>();
        SkipNewLines();
        while (Current.Kind != TokenKind.Eof)
        {
            statements.Add(ParseStatement());
            SkipNewLines();
        }

        return new ProgramNode(statements.ToArray());
    }

    private void ConsumeTerminator()
    {
        if (_profile.NewlineTerminates)
        {
            if (Current.Kind == TokenKind.Eof)
            {
                return;
            }

            if (Current.Kind != TokenKind.NewLine)
            {
                throw new MorphikSyntaxException(
                    "Ожидался конец строки", Current.Line, Current.Column);
            }

            SkipNewLines();
            return;
        }

        if (Current.Kind == TokenKind.Eof)
        {
            return;
        }

        if (Current.Kind == TokenKind.RBrace)
        {
            // A block close may sit where a terminator was expected; tolerate it.
            SkipNewLines();
            return;
        }

        if (Current.Kind == TokenKind.Term)
        {
            Advance();
            SkipNewLines();
            return;
        }

        throw new MorphikSyntaxException(
            $"Ожидался терминатор '{_profile.Terminator}'", Current.Line, Current.Column);
    }

    private MorphikStmt ParseStatement()
    {
        switch (Current.Kind)
        {
            case TokenKind.KwLet:
                {
                    Advance();
                    string name = Expect(TokenKind.Ident, "имя").Text;
                    Expect(TokenKind.OpAssign, "присваивание");
                    MorphikExpr value = ParseExpr();
                    ConsumeTerminator();
                    return new LetStmt(name, value);
                }

            case TokenKind.KwSet:
                {
                    Advance();
                    string name = Expect(TokenKind.Ident, "имя").Text;
                    Expect(TokenKind.OpAssign, "присваивание");
                    MorphikExpr value = ParseExpr();
                    ConsumeTerminator();
                    return new AssignStmt(name, value);
                }

            case TokenKind.KwEmit:
                {
                    Advance();
                    MorphikExpr? value = IsTerminatorLike() ? null : ParseExpr();
                    ConsumeTerminator();
                    return new EmitStmt(value);
                }

            case TokenKind.KwWindow:
                {
                    Advance();
                    MorphikExpr? value = IsTerminatorLike() ? null : ParseExpr();
                    ConsumeTerminator();
                    return new WindowStmt(value);
                }

            case TokenKind.KwReturn:
                {
                    Advance();
                    MorphikExpr? value = IsTerminatorLike() ? null : ParseExpr();
                    ConsumeTerminator();
                    return new ReturnStmt(value);
                }

            case TokenKind.KwBreak:
                {
                    Advance();
                    ConsumeTerminator();
                    return new BreakStmt();
                }

            case TokenKind.KwIf:
                return ParseIf();

            case TokenKind.KwWhile:
                {
                    Advance();
                    MorphikExpr cond = ParseExpr();
                    MorphikStmt[] body = ParseBlock();
                    ConsumeTerminator();
                    return new WhileStmt(cond, body);
                }

            case TokenKind.Ident when Peek().Kind == TokenKind.OpAssign:
                {
                    string name = Advance().Text;
                    Advance(); // assign
                    MorphikExpr value = ParseExpr();
                    ConsumeTerminator();
                    return new AssignStmt(name, value);
                }

            default:
                {
                    MorphikExpr expr = ParseExpr();
                    ConsumeTerminator();
                    return new ExprStmt(expr);
                }
        }
    }

    private MorphikStmt ParseIf()
    {
        Expect(TokenKind.KwIf, _profile.KwIf);
        MorphikExpr cond = ParseExpr();
        MorphikStmt[] then = ParseBlock();

        // Peek past any newlines for an else. When newlines terminate statements we must
        // remember whether we consumed the terminator so we do not ask for it twice.
        int save = _pos;
        SkipNewLines();
        bool consumedNewline = _pos != save;

        MorphikStmt[] els = Array.Empty<MorphikStmt>();
        bool hadElse = Current.Kind == TokenKind.KwElse;
        bool terminatorHandled = false;
        if (hadElse)
        {
            Advance();
            SkipNewLines();
            if (Current.Kind == TokenKind.KwIf)
            {
                els = new MorphikStmt[] { ParseIf() };
                terminatorHandled = true;
            }
            else
            {
                els = ParseBlock();
            }
        }
        else if (_profile.NewlineTerminates && consumedNewline)
        {
            terminatorHandled = true;
        }

        if (!terminatorHandled)
        {
            ConsumeTerminator();
        }

        return new IfStmt(cond, then, els);
    }

    private MorphikStmt[] ParseBlock()
    {
        Expect(TokenKind.LBrace, _profile.BlockOpen);
        SkipNewLines();
        var body = new List<MorphikStmt>();
        while (Current.Kind != TokenKind.RBrace && Current.Kind != TokenKind.Eof)
        {
            body.Add(ParseStatement());
            SkipNewLines();
        }

        Expect(TokenKind.RBrace, _profile.BlockClose);
        return body.ToArray();
    }

    private bool IsTerminatorLike()
        => Current.Kind is TokenKind.Eof or TokenKind.NewLine or TokenKind.RBrace or TokenKind.Term;

    private MorphikExpr ParseExpr() => ParseOr();

    private MorphikExpr ParseOr()
    {
        MorphikExpr left = ParseAnd();
        while (Current.Kind == TokenKind.KwOr)
        {
            Advance();
            left = new BinaryExpr(BinaryOp.Or, left, ParseAnd());
        }

        return left;
    }

    private MorphikExpr ParseAnd()
    {
        MorphikExpr left = ParseCmp();
        while (Current.Kind == TokenKind.KwAnd)
        {
            Advance();
            left = new BinaryExpr(BinaryOp.And, left, ParseCmp());
        }

        return left;
    }

    private MorphikExpr ParseCmp()
    {
        MorphikExpr left = ParseAdd();
        while (true)
        {
            BinaryOp op;
            switch (Current.Kind)
            {
                case TokenKind.OpEq: op = BinaryOp.Eq; break;
                case TokenKind.OpNe: op = BinaryOp.Ne; break;
                case TokenKind.OpLt: op = BinaryOp.Lt; break;
                case TokenKind.OpGt: op = BinaryOp.Gt; break;
                case TokenKind.OpLe: op = BinaryOp.Le; break;
                case TokenKind.OpGe: op = BinaryOp.Ge; break;
                default: return left;
            }

            Advance();
            left = new BinaryExpr(op, left, ParseAdd());
        }
    }

    private MorphikExpr ParseAdd()
    {
        MorphikExpr left = ParseMul();
        while (Current.Kind is TokenKind.OpAdd or TokenKind.OpSub)
        {
            BinaryOp op = Current.Kind == TokenKind.OpAdd ? BinaryOp.Add : BinaryOp.Sub;
            Advance();
            left = new BinaryExpr(op, left, ParseMul());
        }

        return left;
    }

    private MorphikExpr ParseMul()
    {
        MorphikExpr left = ParseUnary();
        while (Current.Kind is TokenKind.OpMul or TokenKind.OpDiv or TokenKind.OpMod)
        {
            BinaryOp op = Current.Kind switch
            {
                TokenKind.OpMul => BinaryOp.Mul,
                TokenKind.OpDiv => BinaryOp.Div,
                _ => BinaryOp.Mod,
            };
            Advance();
            left = new BinaryExpr(op, left, ParseUnary());
        }

        return left;
    }

    private MorphikExpr ParseUnary()
    {
        if (Current.Kind == TokenKind.KwNot)
        {
            Advance();
            return new UnaryExpr(UnaryOp.Not, ParseUnary());
        }

        if (Current.Kind == TokenKind.OpSub)
        {
            Advance();
            return new UnaryExpr(UnaryOp.Neg, ParseUnary());
        }

        return ParsePrimary();
    }

    private MorphikExpr ParsePrimary()
    {
        switch (Current.Kind)
        {
            case TokenKind.Number:
                return new NumberExpr(Advance().Number);

            case TokenKind.String:
                return new StringExpr(Advance().String);

            case TokenKind.KwTrue:
                Advance();
                return new NumberExpr(1);

            case TokenKind.KwFalse:
                Advance();
                return new NumberExpr(0);

            case TokenKind.Ident:
                {
                    string name = Advance().Text;
                    if (Current.Kind == TokenKind.LParen)
                    {
                        Advance();
                        var args = new List<MorphikExpr>();
                        SkipNewLines();
                        if (Current.Kind != TokenKind.RParen)
                        {
                            args.Add(ParseExpr());
                            while (Current.Kind == TokenKind.Comma)
                            {
                                Advance();
                                args.Add(ParseExpr());
                            }
                        }

                        Expect(TokenKind.RParen, _profile.ParenClose);
                        return new CallExpr(name, args.ToArray());
                    }

                    return new IdentExpr(name);
                }

            case TokenKind.LParen:
                {
                    Advance();
                    MorphikExpr inner = ParseExpr();
                    Expect(TokenKind.RParen, _profile.ParenClose);
                    return inner;
                }

            default:
                throw new MorphikSyntaxException(
                    $"Неожиданный элемент {Current}", Current.Line, Current.Column);
        }
    }
}
