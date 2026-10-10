namespace Scp603.Core.Morphik;

/// <summary>Result of running VMs; lets callers stop a runaway program deterministically.</summary>
public interface IMorphikHost
{
    void Emit(object? value);

    void Window(object? value);

    bool ShouldStop { get; }
}

/// <summary>A host that records nothing — used for probing.</summary>
public sealed class NullMorphikHost : IMorphikHost
{
    public static readonly NullMorphikHost Instance = new();

    public void Emit(object? value)
    {
    }

    public void Window(object? value)
    {
    }

    public bool ShouldStop => false;
}
