using System.Text;

namespace Scp603.Core.Morphik;

/// <summary>
/// Turns Morphik text into tokens using the spelling tables of one specific version.
/// </summary>
public sealed class Lexer
{
    private readonly MorphikProfile _profile;
    private readonly Dictionary<string, TokenKind> _words = new(StringComparer.Ordinal);
    private readonly List<(string Text, TokenKind Kind)> _symbols = new();

    public Lexer(MorphikProfile profile)
    {
        _profile = profile;
        BuildTables();
    }

    private void BuildTables()
    {
        void Add(TokenKind kind, string spelling)
        {
            if (spelling.Length == 0)
            {
                return;
            }

            bool allLetters = true;
            foreach (char c in spelling)
            {
                if (!char.IsLetter(c))
                {
                    allLetters = false;
                    break;
                }
            }

            if (allLetters)
            {
                _words[spelling] = kind;
            }
            else
            {
                _symbols.Add((spelling, kind));
            }
        }

        MorphikProfile p = _profile;
        Add(TokenKind.KwLet, p.KwLet);
        Add(TokenKind.KwSet, p.KwSet);
        Add(TokenKind.KwEmit, p.KwEmit);
        Add(TokenKind.KwIf, p.KwIf);
        Add(TokenKind.KwElse, p.KwElse);
        Add(TokenKind.KwWhile, p.KwWhile);
        Add(TokenKind.KwReturn, p.KwReturn);
        Add(TokenKind.KwBreak, p.KwBreak);
        Add(TokenKind.KwWindow, p.KwWindow);
        Add(TokenKind.KwTrue, p.KwTrue);
        Add(TokenKind.KwFalse, p.KwFalse);
        Add(TokenKind.KwAnd, p.KwAnd);
        Add(TokenKind.KwOr, p.KwOr);
        Add(TokenKind.KwNot, p.KwNot);

        Add(TokenKind.OpAssign, p.OpAssign);
        Add(TokenKind.OpAdd, p.OpAdd);
        Add(TokenKind.OpSub, p.OpSub);
        Add(TokenKind.OpMul, p.OpMul);
        Add(TokenKind.OpDiv, p.OpDiv);
        Add(TokenKind.OpMod, p.OpMod);
        Add(TokenKind.OpEq, p.OpEq);
        Add(TokenKind.OpNe, p.OpNe);
        Add(TokenKind.OpLt, p.OpLt);
        Add(TokenKind.OpGt, p.OpGt);
        Add(TokenKind.OpLe, p.OpLe);
        Add(TokenKind.OpGe, p.OpGe);

        Add(TokenKind.LBrace, p.BlockOpen);
        Add(TokenKind.RBrace, p.BlockClose);
        Add(TokenKind.LParen, p.ParenOpen);
        Add(TokenKind.RParen, p.ParenClose);
        Add(TokenKind.Comma, p.ArgSep);

        if (!p.NewlineTerminates)
        {
            Add(TokenKind.Term, p.Terminator);
        }

        // Longest first so that '==' wins over '='.
        _symbols.Sort((a, b) => b.Text.Length.CompareTo(a.Text.Length));
    }

    public List<Token> Lex(string source)
    {
        var tokens = new List<Token>();
        int i = 0;
        int line = 1;
        int col = 1;
        int n = source.Length;

        while (i < n)
        {
            char c = source[i];

            // Comments are checked before operators so that e.g. "//" beats "/".
            if (StartsWith(source, i, _profile.CommentPrefix))
            {
                while (i < n && source[i] != '\n')
                {
                    i++;
                    col++;
                }

                continue;
            }

            if (c == '\n')
            {
                if (_profile.NewlineTerminates)
                {
                    tokens.Add(Token.Make(TokenKind.NewLine, "\n", line, col));
                }

                i++;
                line++;
                col = 1;
                continue;
            }

            if (c == '\r' || c == ' ' || c == '\t')
            {
                i++;
                col++;
                continue;
            }

            if (c == _profile.IdentifierPrefix)
            {
                int start = i;
                int startCol = col;
                i++;
                col++;
                var sb = new StringBuilder();
                while (i < n && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                {
                    sb.Append(source[i]);
                    i++;
                    col++;
                }

                if (sb.Length == 0)
                {
                    throw new MorphikSyntaxException("Пустой идентификатор", line, startCol);
                }

                tokens.Add(new Token(TokenKind.Ident, sb.ToString(), 0, string.Empty, line, startCol));
                _ = start;
                continue;
            }

            if (c == _profile.StringQuote)
            {
                int startCol = col;
                i++;
                col++;
                var sb = new StringBuilder();
                while (i < n && source[i] != _profile.StringQuote)
                {
                    if (source[i] == '\\' && i + 1 < n)
                    {
                        char next = source[i + 1];
                        sb.Append(next switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            _ => next,
                        });
                        i += 2;
                        col += 2;
                        continue;
                    }

                    sb.Append(source[i]);
                    i++;
                    col++;
                }

                if (i >= n)
                {
                    throw new MorphikSyntaxException("Незакрытая строка", line, startCol);
                }

                i++; // closing quote
                col++;
                tokens.Add(new Token(TokenKind.String, sb.ToString(), 0, sb.ToString(), line, startCol));
                continue;
            }

            if (IsDigitInBase(c, _profile.NumericBase))
            {
                long value = 0;
                int startCol = col;
                while (i < n && IsDigitInBase(source[i], _profile.NumericBase))
                {
                    value = value * _profile.NumericBase + DigitValue(source[i]);
                    i++;
                    col++;
                }

                tokens.Add(new Token(TokenKind.Number, string.Empty, value, string.Empty, line, startCol));
                continue;
            }

            bool matched = false;
            foreach ((string text, TokenKind kind) in _symbols)
            {
                if (StartsWith(source, i, text))
                {
                    tokens.Add(Token.Make(kind, text, line, col));
                    i += text.Length;
                    col += text.Length;
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                continue;
            }

            if (char.IsLetter(c))
            {
                int startCol = col;
                var sb = new StringBuilder();
                while (i < n && char.IsLetter(source[i]))
                {
                    sb.Append(source[i]);
                    i++;
                    col++;
                }

                if (_words.TryGetValue(sb.ToString(), out TokenKind kind))
                {
                    tokens.Add(Token.Make(kind, sb.ToString(), line, startCol));
                    continue;
                }

                throw new MorphikSyntaxException($"Неизвестное слово '{sb}'", line, startCol);
            }

            throw new MorphikSyntaxException($"Неизвестный символ '{c}'", line, col);
        }

        tokens.Add(Token.Make(TokenKind.Eof, string.Empty, line, col));
        return tokens;
    }

    private static bool StartsWith(string source, int index, string value)
    {
        if (value.Length == 0 || index + value.Length > source.Length)
        {
            return false;
        }

        for (int k = 0; k < value.Length; k++)
        {
            if (source[index + k] != value[k])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDigitInBase(char c, int numericBase)
    {
        int v = DigitValue(c);
        return v >= 0 && v < numericBase;
    }

    private static int DigitValue(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'a' && c <= 'z')
        {
            return 10 + (c - 'a');
        }

        if (c >= 'A' && c <= 'Z')
        {
            return 10 + (c - 'A');
        }

        return -1;
    }
}
