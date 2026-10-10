namespace Scp603.Core.Scenes;

/// <summary>
/// A side effect a scene wants the host to perform (write a file, log a narrative, ...).
/// Scenes never touch the outside world directly, which keeps them safe and testable.
/// </summary>
public sealed record SceneEffect(string Kind, string Payload);

public interface ISceneEffects
{
    /// <summary>Returns and clears any pending effects.</summary>
    IReadOnlyList<SceneEffect> DrainEffects();
}
