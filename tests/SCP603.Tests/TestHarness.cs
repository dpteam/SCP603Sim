namespace Scp603.Tests;

/// <summary>
/// A dependency-free test harness. Keeps the "no third-party libraries" rule while still
/// giving us real, runnable verification instead of judging by screenshots.
/// </summary>
public static class TestHarness
{
    private static readonly List<(string Name, Action Body)> Tests = new();

    public static void Register(string name, Action body) => Tests.Add((name, body));

    public static int RunAll(string? filter = null)
    {
        int passed = 0;
        var failures = new List<(string Name, Exception Error)>();

        foreach ((string name, Action body) in Tests)
        {
            if (filter is not null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                body();
                passed++;
                Console.WriteLine($"  PASS  {name}");
            }
            catch (Exception ex)
            {
                failures.Add((name, ex));
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"        {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Итог: {passed} прошло, {failures.Count} провалено, всего {passed + failures.Count}.");
        return failures.Count;
    }
}

public sealed class AssertionException : Exception
{
    public AssertionException(string message)
        : base(message)
    {
    }
}

public static class Check
{
    public static void True(bool condition, string message = "ожидалось true")
    {
        if (!condition)
        {
            throw new AssertionException(message);
        }
    }

    public static void False(bool condition, string message = "ожидалось false")
    {
        if (condition)
        {
            throw new AssertionException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string message = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException($"{message} ожидалось <{expected}>, получено <{actual}>".Trim());
        }
    }

    public static void NotEqual<T>(T notExpected, T actual, string message = "")
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            throw new AssertionException($"{message} значение не должно было быть <{actual}>".Trim());
        }
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message = "")
    {
        T[] e = expected.ToArray();
        T[] a = actual.ToArray();
        if (e.Length != a.Length)
        {
            throw new AssertionException($"{message} длины различаются: {e.Length} != {a.Length}".Trim());
        }

        for (int i = 0; i < e.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(e[i], a[i]))
            {
                throw new AssertionException($"{message} элемент [{i}]: ожидалось <{e[i]}>, получено <{a[i]}>".Trim());
            }
        }
    }

    public static void Contains(string haystack, string needle, string message = "")
    {
        if (!haystack.Contains(needle, StringComparison.Ordinal))
        {
            throw new AssertionException($"{message} строка не содержит «{needle}»".Trim());
        }
    }

    public static void NotEmpty(string value, string message = "строка пуста")
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new AssertionException(message);
        }
    }

    public static void Throws<TException>(Action action, string message = "")
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"{message} ожидалось {typeof(TException).Name}, получено {ex.GetType().Name}".Trim());
        }

        throw new AssertionException($"{message} ожидалось исключение {typeof(TException).Name}".Trim());
    }
}
