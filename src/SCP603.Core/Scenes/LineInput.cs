using System.Text;

namespace Scp603.Core.Scenes;

/// <summary>Reusable single-line text input used by the interactive scenes.</summary>
public sealed class LineInput
{
    private readonly StringBuilder _buffer = new();

    public LineInput(int maxLength = int.MaxValue)
    {
        MaxLength = maxLength;
    }

    public int MaxLength { get; }

    public string Text => _buffer.ToString();

    public bool IsEmpty => _buffer.Length == 0;

    public bool IsFull => _buffer.Length >= MaxLength;

    public void Type(char c)
    {
        if (char.IsControl(c) || IsFull)
        {
            return;
        }

        _buffer.Append(c);
    }

    public void Backspace()
    {
        if (_buffer.Length > 0)
        {
            _buffer.Length--;
        }
    }

    public void Clear() => _buffer.Clear();

    public void Set(string value)
    {
        _buffer.Clear();
        foreach (char c in value)
        {
            Type(c);
        }
    }

    /// <summary>Feeds an input event; invokes <paramref name="submit"/> on Enter.</summary>
    public void Feed(in SceneInput input, Action<string> submit)
    {
        switch (input.Kind)
        {
            case SceneInputKind.Text when input.Text is "\n" or "\r" or "Enter":
                submit(Text);
                Clear();
                break;

            case SceneInputKind.Text when input.Text is "\u0008" or "Backspace":
                Backspace();
                break;

            case SceneInputKind.Text when input.Text.Length == 1:
                Type(input.Text[0]);
                break;

            case SceneInputKind.Text when input.Text.Length > 1:
                Set(input.Text);
                submit(Text);
                Clear();
                break;

            case SceneInputKind.Key when input.Key is "Enter" or "Return":
                submit(Text);
                Clear();
                break;

            case SceneInputKind.Key when input.Key == "Backspace":
                Backspace();
                break;
        }
    }
}
