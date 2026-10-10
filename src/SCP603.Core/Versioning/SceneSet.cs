using Scp603.Core.Morphik;
using Scp603.Core.Numerics;
using Scp603.Core.Scenes;

namespace Scp603.Core.Versioning;

/// <summary>
/// The set of scenes a particular version of SCP-603 can open, in the order it will open them.
/// Deterministic: replaying the same version gives the same windows on the same primes.
/// </summary>
public sealed class SceneSet
{
    private readonly string[] _sequence;

    public SceneSet(int version, string[] sequence)
    {
        Version = version;
        _sequence = sequence.Length > 0 ? sequence : new[] { SceneCatalog.DieId };
    }

    public int Version { get; }

    public IReadOnlyList<string> Sequence => _sequence;

    /// <summary>1-based window number =&gt; scene id.</summary>
    public string SceneForWindow(int windowNumber)
    {
        int idx = (windowNumber - 1) % _sequence.Length;
        if (idx < 0)
        {
            idx += _sequence.Length;
        }

        return _sequence[idx];
    }

    public static SceneSet Build(MorphikProfile profile)
    {
        var rng = new XorShift64(unchecked(0x5CE7EUL ^ profile.Digest() ^ ((ulong)profile.Version * 0xD1B54A32D192ED03UL)));

        var chosen = new List<string> { SceneCatalog.DieId }; // "Умереть" has never changed — always present.
        var pool = SceneCatalog.AllIds.Where(id => id != SceneCatalog.DieId).ToList();

        // Shuffle the pool deterministically and take a handful.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int take = rng.NextInt(4, Math.Min(9, pool.Count) + 1);
        chosen.AddRange(pool.Take(take));

        // Occasionally a scene recurs, as some do across versions.
        if (rng.Chance(0.5))
        {
            chosen.Add(rng.Pick(chosen.ToArray()));
        }

        return new SceneSet(profile.Version, chosen.ToArray());
    }
}
