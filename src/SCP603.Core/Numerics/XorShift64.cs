namespace Scp603.Core.Numerics;

/// <summary>
/// Small, fast, fully deterministic pseudo random generator.
/// The whole simulator relies on this so that "the same version always behaves the same way".
/// No external dependencies.
/// </summary>
public sealed class XorShift64
{
    private ulong _state;

    public XorShift64(ulong seed)
    {
        // A zero state would produce a zero stream; use a fixed odd constant instead.
        _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public ulong NextUInt64()
    {
        ulong x = _state;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        _state = x;
        return x;
    }

    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        ulong range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt64() % range);
    }

    public long NextInt64(long minInclusive, long maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        ulong range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (long)(NextUInt64() % range);
    }

    public bool NextBool() => (NextUInt64() & 1UL) != 0UL;

    /// <summary>True with probability <paramref name="probability"/> (0..1).</summary>
    public bool Chance(double probability) => NextDouble() < probability;

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    public T Pick<T>(IReadOnlyList<T> items) => items[NextInt(0, items.Count)];

    public ulong State => _state;

    /// <summary>Creates a deterministic child generator derived from the current state.</summary>
    public XorShift64 Fork(ulong salt) => new(unchecked(_state * 0x2545F4914F6CDD1DUL) ^ (salt + 0xD1B54A32D192ED03UL));
}
