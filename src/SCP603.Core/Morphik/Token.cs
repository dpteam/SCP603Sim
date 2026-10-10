namespace Scp603.Core.Morphik;

/// <summary>A single lexical unit of Morphik source.</summary>
public readonly record struct Token(
    TokenKind Kind,
    string Text,
    long Number,
    string String,
    int Line,
    int Column)
{
    public static Token Make(TokenKind kind, string text, int line, int col)
        => new(kind, text, 0, string.Empty, line, col);

    public override string ToString() => Kind switch
    {
        TokenKind.Number => $"Number({Number})",
        TokenKind.String => $"String({String})",
        TokenKind.Ident => $"Ident({Text})",
        TokenKind.Eof => "EOF",
        TokenKind.NewLine => "NL",
        _ => $"{Kind}('{Text}')",
    };
}
