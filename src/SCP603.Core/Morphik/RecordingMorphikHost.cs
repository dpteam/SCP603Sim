namespace Scp603.Core.Morphik;

/// <summary>Host that records everything a compiled Morphik program emits.</summary>
public sealed class RecordingMorphikHost : IMorphikHost
{
    public List<object?> Emitted { get; } = new();

    public List<object?> Windows { get; } = new();

    public bool ShouldStop { get; set; }

    public bool StopAfterWindows { get; set; }

    public int StopAfterEmits { get; set; }

    public void Emit(object? value)
    {
        Emitted.Add(value);
        if (StopAfterEmits > 0 && Emitted.Count >= StopAfterEmits)
        {
            ShouldStop = true;
        }
    }

    public void Window(object? value)
    {
        Windows.Add(value);
        if (StopAfterWindows && Windows.Count > 0)
        {
            ShouldStop = true;
        }
    }
}
