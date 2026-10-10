using Scp603.Core.Morphik;
using Scp603.Core.Numerics;
using static Scp603.Tests.Check;
using static Scp603.Tests.TestHarness;

namespace Scp603.Tests;

public static class MorphikTests
{
    public static void RegisterAll()
    {
        Register("morphik: профиль версии детерминирован", () =>
        {
            MorphikProfile a = MorphikProfileFactory.ForVersion(17);
            MorphikProfile b = MorphikProfileFactory.ForVersion(17);
            Equal(a.Digest(), b.Digest());

            int distinct = Enumerable.Range(1, 40)
                .Select(v => MorphikProfileFactory.ForVersion(v).Digest())
                .Distinct()
                .Count();
            True(distinct > 30, $"язык должен меняться: различных профилей {distinct}");
        });

        Register("morphik: генерация и разбор дают одно дерево (v1..v60)", () =>
        {
            for (int v = 1; v <= 60; v++)
            {
                MorphikProfile profile = MorphikProfileFactory.ForVersion(v);
                ProgramNode program = ProgramTemplate.Build(profile);
                string source = CodeGenerator.Generate(profile, program);
                ProgramNode reparsed;
                try
                {
                    reparsed = Parser.Parse(profile, source);
                }
                catch (Exception ex)
                {
                    throw new AssertionException($"v{v}: {ex.Message}\n--- source ---\n{source}");
                }

                True(AstComparer.ProgramEquals(program, reparsed), $"v{v}: дерево не совпало");
                string again = CodeGenerator.Generate(profile, reparsed);
                Equal(source, again, $"v{v}: повторная генерация отличается");
            }
        });

        Register("morphik: деобфускация сохраняет смысл (v1..v40)", () =>
        {
            for (int v = 1; v <= 40; v++)
            {
                MorphikProfile profile = MorphikProfileFactory.ForVersion(v);
                ProgramNode program = ProgramTemplate.Build(profile);
                ProgramNode obfuscated = MorphikObfuscator.Obfuscate(program, new XorShift64((ulong)v ^ 0xBEEF));

                string source = CodeGenerator.Generate(profile, obfuscated);
                ProgramNode reparsed = Parser.Parse(profile, source);
                True(AstComparer.ProgramEquals(obfuscated, reparsed), $"v{v}: обфусцированное дерево не совпало");

                long[] plain = RunProgram(Compile(profile, program), 40);
                long[] obf = RunProgram(Compile(profile, obfuscated), 40);
                SequenceEqual(plain, obf, $"v{v}: обфускация изменила поведение");
            }
        });

        Register("morphik: VM воспроизводит простые числа и окна (v1..v20)", () =>
        {
            for (int v = 1; v <= 20; v++)
            {
                MorphikProfile profile = MorphikProfileFactory.ForVersion(v);
                ProgramNode program = ProgramTemplate.Build(profile);
                MorphikBytecode bytecode = MorphikCompiler.Compile(program);

                var host = new RecordingMorphikHost { StopAfterEmits = 60 };
                new MorphikVm { StepLimit = 5_000_000 }.Run(bytecode, host);

                (List<long> refEmits, List<long> refWindows) = Reference(profile, 60);

                SequenceEqual(refEmits, host.Emitted.Cast<long>(), $"v{v}: вывод простых чисел");
                SequenceEqual(refWindows, host.Windows.Cast<long>(), $"v{v}: окна");
            }
        });

        Register("morphik: терминатор-символ тоже разбирается", () =>
        {
            // Force a profile with a symbolic terminator by replaying until we find one.
            MorphikProfile? found = null;
            for (int v = 1; v <= 200 && found is null; v++)
            {
                MorphikProfile p = MorphikProfileFactory.ForVersion(v);
                if (!p.NewlineTerminates)
                {
                    found = p;
                }
            }

            True(found is not null, "не найдено ни одной версии с символьным терминатором");
            ProgramNode program = ProgramTemplate.Build(found!);
            string source = CodeGenerator.Generate(found!, program);
            ProgramNode reparsed = Parser.Parse(found!, source);
            True(AstComparer.ProgramEquals(program, reparsed));
        });

        Register("morphik: лексер отвергает чуждую версии грамматику", () =>
        {
            MorphikProfile v1 = MorphikProfileFactory.ForVersion(1);
            // A random Cyrillic word that is not a keyword must raise.
            Throws<MorphikSyntaxException>(() => Parser.Parse(v1, "совершеннo непонятное слово;"));
        });

        Register("morphik: FormatValue учитывает стиль вывода", () =>
        {
            var p = MorphikProfileFactory.ForVersion(1);
            p.Emit = EmitStyle.Hex;
            Equal("ff", MorphikToolchain.FormatValue(255L, p));
            p.Emit = EmitStyle.Binary;
            Equal("1010", MorphikToolchain.FormatValue(10L, p));
            p.Emit = EmitStyle.Decimal;
            Equal("42", MorphikToolchain.FormatValue(42L, new MorphikProfile { NumericBase = 10, Emit = EmitStyle.Decimal }));
            Equal("52", MorphikToolchain.FormatValue(42L, new MorphikProfile { NumericBase = 8, Emit = EmitStyle.Decimal }));
            Equal("101010", MorphikToolchain.FormatValue(42L, new MorphikProfile { NumericBase = 2, Emit = EmitStyle.Decimal }));
        });
    }

    private static MorphikBytecode Compile(MorphikProfile profile, ProgramNode program)
        => MorphikCompiler.Compile(program);

    private static long[] RunProgram(MorphikBytecode bytecode, int count)
    {
        var host = new RecordingMorphikHost { StopAfterEmits = count };
        new MorphikVm { StepLimit = 5_000_000 }.Run(bytecode, host);
        return host.Emitted.Select(x => Convert.ToInt64(x)).ToArray();
    }

    private static (List<long> Emits, List<long> Windows) Reference(MorphikProfile profile, int emitCount)
    {
        var emits = new List<long>();
        var windows = new List<long>();
        long value = profile.StartValue;
        int index = profile.IndexOneBased ? 1 : 0;
        while (emits.Count < emitCount)
        {
            if (Primes.IsPrime(value))
            {
                emits.Add(value);
                if (Primes.IsPrime(index))
                {
                    windows.Add(value);
                }

                index++;
            }

            value++;
        }

        return (emits, windows);
    }
}
