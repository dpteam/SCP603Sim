namespace Scp603.Core.Numerics;

/// <summary>
/// Pure, allocation friendly prime helpers.
/// The main thread of SCP-603 "generates primes from 2 and upwards" — this is that engine.
/// </summary>
public static class Primes
{
    /// <summary>Deterministic Miller-Rabin check for a signed 64-bit value.</summary>
    public static bool IsPrime(long value)
    {
        if (value < 2)
        {
            return false;
        }

        foreach (int small in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
        {
            if (value == small)
            {
                return true;
            }

            if (value % small == 0)
            {
                return false;
            }
        }

        long d = value - 1;
        int s = 0;
        while ((d & 1) == 0)
        {
            d >>= 1;
            s++;
        }

        // These bases are a deterministic set valid for all 64-bit unsigned integers.
        ulong n = (ulong)value;
        foreach (ulong a in new ulong[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
        {
            if (a % n == 0)
            {
                continue;
            }

            ulong x = PowMod(a, (ulong)d, n);
            if (x == 1 || x == n - 1)
            {
                continue;
            }

            bool composite = true;
            for (int r = 1; r < s; r++)
            {
                x = MulMod(x, x, n);
                if (x == n - 1)
                {
                    composite = false;
                    break;
                }
            }

            if (composite)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong MulMod(ulong a, ulong b, ulong mod)
    {
        // 128-bit multiply via Math.BigMul (available since .NET 5) to avoid overflow.
        ulong hi = Math.BigMul(a, b, out ulong lo);
        return Mod128(hi, lo, mod);
    }

    private static ulong Mod128(ulong hi, ulong lo, ulong mod)
    {
        if (hi == 0)
        {
            return lo % mod;
        }

        // Reduce (hi:lo) mod mod using shift-and-subtract. Slow but only used for very large values.
        const int bits = 64;
        ulong result = hi % mod;
        for (int i = bits - 1; i >= 0; i--)
        {
            result = (result << 1) % mod;
            if (((lo >> i) & 1UL) != 0)
            {
                result = (result + (1 % mod)) % mod;
            }
        }

        return result;
    }

    private static ulong PowMod(ulong baseValue, ulong exponent, ulong mod)
    {
        ulong result = 1 % mod;
        ulong b = baseValue % mod;
        while (exponent > 0)
        {
            if ((exponent & 1UL) != 0)
            {
                result = MulMod(result, b, mod);
            }

            b = MulMod(b, b, mod);
            exponent >>= 1;
        }

        return result;
    }

    /// <summary>The <paramref name="index"/>-th prime (0-based: index 0 =&gt; 2).</summary>
    public static long Nth(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        long candidate = 1;
        int found = -1;
        while (true)
        {
            candidate++;
            if (IsPrime(candidate))
            {
                found++;
                if (found == index)
                {
                    return candidate;
                }
            }
        }
    }

    /// <summary>Zero-based position of <paramref name="prime"/> in the sequence of primes, or -1.</summary>
    public static int IndexOf(long prime)
    {
        if (!IsPrime(prime))
        {
            return -1;
        }

        int index = 0;
        for (long v = 2; v < prime; v++)
        {
            if (IsPrime(v))
            {
                index++;
            }
        }

        return index;
    }

    /// <summary>
    /// Lazily enumerates prime numbers starting at 2.
    /// </summary>
    public static IEnumerable<long> Sequence(long start = 2)
    {
        long candidate = start < 2 ? 2 : start;
        if (candidate == 1)
        {
            candidate = 2;
        }

        while (true)
        {
            if (IsPrime(candidate))
            {
                yield return candidate;
            }

            if (candidate >= long.MaxValue - 2)
            {
                yield break; // numeric exhaustion guard
            }

            candidate++;
        }
    }
}
