namespace Scp603.Core.Morphik;

/// <summary>Raised when a stream of bytes cannot be interpreted by the current Morphik version.</summary>
public sealed class MorphikSyntaxException : Exception
{
    public MorphikSyntaxException(string message, int line, int column)
        : base($"{message} (строка {line}, позиция {column})")
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}
