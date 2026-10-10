using Scp603.Core.Numerics;
using static Scp603.Tests.Check;
using static Scp603.Tests.TestHarness;

namespace Scp603.Tests;

public static class PrimesTests
{
    public static void RegisterAll()
    {
        Register("primes: первые 25 простых чисел", () =>
        {
            long[] expected = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97 };
            SequenceEqual(expected, Primes.Sequence().Take(25));
        });

        Register("primes: граничные случаи простоты", () =>
        {
            False(Primes.IsPrime(-7));
            False(Primes.IsPrime(0));
            False(Primes.IsPrime(1));
            True(Primes.IsPrime(2));
            True(Primes.IsPrime(3));
            False(Primes.IsPrime(4));
            True(Primes.IsPrime(97));
            False(Primes.IsPrime(100));
            True(Primes.IsPrime(104729)); // 10000-е простое
            False(Primes.IsPrime(104730));
            True(Primes.IsPrime(2147483647)); // 2^31-1, простое Мерсенна
        });

        Register("primes: Nth и IndexOf согласованы", () =>
        {
            Equal(2L, Primes.Nth(0));
            Equal(97L, Primes.Nth(24));
            Equal(541L, Primes.Nth(99));
            Equal(24, Primes.IndexOf(97));
            Equal(0, Primes.IndexOf(2));
            Equal(-1, Primes.IndexOf(98));

            for (int i = 0; i < 50; i++)
            {
                Equal(i, Primes.IndexOf(Primes.Nth(i)));
            }
        });

        Register("random: XorShift64 детерминирован", () =>
        {
            var a = new XorShift64(123456789);
            var b = new XorShift64(123456789);
            for (int i = 0; i < 1000; i++)
            {
                Equal(a.NextUInt64(), b.NextUInt64());
            }

            var c = new XorShift64(987654321);
            NotEqual(new XorShift64(1).NextUInt64(), c.NextUInt64());
        });

        Register("random: NextInt в пределах диапазона", () =>
        {
            var rng = new XorShift64(42);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(5, 10);
                True(v is >= 5 and < 10, $"значение вне диапазона: {v}");
            }
        });
    }
}
