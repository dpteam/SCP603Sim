using Scp603.Core.Kernel;
using Scp603.Core.Morphik;
using Scp603.Core.Numerics;
using static Scp603.Tests.Check;
using static Scp603.Tests.TestHarness;

namespace Scp603.Tests;

public static class KernelTests
{
    private static KernelOptions Options(string dir) => new()
    {
        BaseDirectory = dir,
        StartVersion = 1,
        ForgeTargetVersions = 5,
        ForgeStepDelayMs = 0,
        PrimeDelayMs = 0,
        JournalToFile = false,
        EnableSourceForge = true,
    };

    public static void RegisterAll()
    {
        Register("kernel: вывод простых чисел детерминирован", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                var first = kernel.GeneratePrimes(200);
                var second = kernel.GeneratePrimes(200);

                SequenceEqual(first.Select(x => x.Prime.Value), second.Select(x => x.Prime.Value));
                SequenceEqual(first.Select(x => x.Window?.SceneId ?? ""), second.Select(x => x.Window?.SceneId ?? ""));

                // First prime must be 2 and the first window must appear at a prime whose index is prime.
                Equal(2L, first[0].Prime.Value);
                WindowEvent? firstWindow = first.Select(x => x.Window).First(w => w is not null);
                True(firstWindow is not null);
                Equal(3L, firstWindow!.Value.Prime);
             }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("kernel: второй поток выводит новые версии в source/", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                IReadOnlyList<VersionEvent> events = kernel.GenerateSources(5);

                Equal(5, events.Count);
                Equal(2, events[0].Version);
                Equal(6, events[^1].Version);

                foreach (VersionEvent evt in events)
                {
                    True(File.Exists(evt.SourcePath), $"нет файла {evt.SourcePath}");
                    string source = File.ReadAllText(evt.SourcePath);
                    MorphikProfile profile = MorphikProfileFactory.ForVersion(evt.Version);
                    ProgramNode program = Parser.Parse(profile, source); // must not throw
                    True(program.Statements.Length >= 3, $"v{evt.Version}: слишком короткая программа");
                }
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("kernel: компиляция файлов Морфика даёт megaprime", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                kernel.GenerateSources(3);
                string file = Path.Combine(kernel.Options.SourceDirectory, "v0002.morphik");
                True(File.Exists(file));

                MegaprimeArtifact artifact = kernel.CompileFiles(new[] { file });
                string megaprime = Path.Combine(kernel.Options.OutputDirectory, "megaprime");
                True(File.Exists(megaprime));
                True(File.Exists(megaprime + ".manifest.txt"));

                var host = new RecordingMorphikHost { StopAfterEmits = 12 };
                new MorphikVm { StepLimit = 1_000_000 }.Run(artifact.Bytecode, host);
                long[] emitted = host.Emitted.Select(Convert.ToInt64).ToArray();
                SequenceEqual(new long[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 }, emitted);
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("kernel: побочные эффекты сцен пишутся в свой каталог", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                kernel.ApplyEffect(new Scp603.Core.Scenes.SceneEffect("print", "ТЕСТ"));
                kernel.ApplyEffect(new Scp603.Core.Scenes.SceneEffect("narrative", "Жила-была история."));

                Equal("ТЕСТ", File.ReadAllText(Path.Combine(kernel.Options.OutputDirectory, "printout.txt")));
                Equal("Жила-была история.", File.ReadAllText(Path.Combine(kernel.Options.OutputDirectory, "narrative.txt")));
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("kernel: определение версии по имени файла", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                Equal(7, kernel.ProfileForFile("v0007.morphik").Version);
                Equal(kernel.Profile.Version, kernel.ProfileForFile("безымянный.morphik").Version);
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("kernel: запуск и остановка потоков не падает", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                int primes = 0;
                kernel.PrimeGenerated += _ => Interlocked.Increment(ref primes);
                kernel.Start();
                True(kernel.IsRunning);
                Thread.Sleep(200);
                kernel.Stop();
                False(kernel.IsRunning);
                True(primes > 0, "поток простых чисел ничего не вывел");
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });
    }
}
