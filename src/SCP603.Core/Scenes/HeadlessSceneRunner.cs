using Scp603.Core.Morphik;

namespace Scp603.Core.Scenes;

/// <summary>
/// Drives scenes without any UI. Used by the CLI and by the tests so that scene logic is
/// verified directly rather than by looking at screenshots.
/// </summary>
public sealed class HeadlessSceneRunner
{
    private readonly SceneHostOptions _options;
    private readonly List<IScene> _active = new();

    public HeadlessSceneRunner(SceneHostOptions? options = null)
    {
        _options = options ?? new SceneHostOptions();
    }

    /// <summary>Raised for every side effect a scene wants performed.</summary>
    public event Action<SceneEffect>? EffectProduced;

    public IReadOnlyList<IScene> Active => _active;

    public IScene Open(string sceneId, MorphikProfile profile, ulong seed)
    {
        IScene scene = SceneCatalog.Create(sceneId, _options);
        scene.Start(profile, seed);
        _active.Add(scene);
        return scene;
    }

    public void UpdateAll(double deltaSeconds)
    {
        foreach (IScene scene in _active.ToArray())
        {
            scene.Update(deltaSeconds);
            Drain(scene);
        }
    }

    public void Update(IScene scene, double deltaSeconds)
    {
        scene.Update(deltaSeconds);
        Drain(scene);
    }

    public void Close(IScene scene)
    {
        scene.Stop();
        _active.Remove(scene);
    }

    public void CloseFinished()
    {
        foreach (IScene scene in _active.ToArray())
        {
            if (scene.Status is SceneStatus.Finished or SceneStatus.Crashed)
            {
                Drain(scene);
                _active.Remove(scene);
            }
        }
    }

    private void Drain(IScene scene)
    {
        if (scene is not ISceneEffects effects)
        {
            return;
        }

        foreach (SceneEffect effect in effects.DrainEffects())
        {
            EffectProduced?.Invoke(effect);
        }
    }
}
