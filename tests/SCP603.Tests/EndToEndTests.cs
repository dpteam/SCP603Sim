using Scp603.Core.Kernel;
using Scp603.Core.Morphik;
using Scp603.Core.Scenes;
using static Scp603.Tests.Check;
using static Scp603.Tests.TestHarness;

namespace Scp603.Tests;

public static class EndToEndTests
{
    private static KernelOptions Options(string dir) => new()
    {
        BaseDirectory = dir,
        StartVersion = 1,
        ForgeTargetVersions = 4,
        ForgeStepDelayMs = 0,
        JournalToFile = false,
    };

    public static void RegisterAll()
    {
        Register("e2e: полный проход порождает рабочий megaprime", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                kernel.GenerateSources(4);

                string source = Path.Combine(kernel.Options.SourceDirectory, "v0003.morphik");
                MegaprimeArtifact artifact = kernel.CompileFiles(new[] { source });

                var host = new RecordingMorphikHost();
                new MorphikVm { StepLimit = 3_000_000, }.Run(artifact.Bytecode, host);

                True(host.Emitted.Count > 50, "megaprime вывел слишком мало чисел");
                // The first emitted values must be primes in ascending order.
                long previous = 1;
                foreach (object? v in host.Emitted.Take(50))
                {
                    long value = Convert.ToInt64(v);
                    True(value > previous, "числа не возрастают");
                    previous = value;
                }
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("e2e: две одинаковые конфигурации дают идентичные исходники", () =>
        {
            string dirA = TestFs.NewTempDir();
            string dirB = TestFs.NewTempDir();
            try
            {
                using (var kernelA = new Scp603Kernel(Options(dirA)))
                {
                    kernelA.GenerateSources(3);
                }

                using (var kernelB = new Scp603Kernel(Options(dirB)))
                {
                    kernelB.GenerateSources(3);
                }

                for (int v = 2; v <= 4; v++)
                {
                    string a = File.ReadAllText(Path.Combine(dirA, "source", $"v{v:D4}.morphik"));
                    string b = File.ReadAllText(Path.Combine(dirB, "source", $"v{v:D4}.morphik"));
                    Equal(a, b, $"версия {v} отличается между запусками");
                }
            }
            finally
            {
                TestFs.Cleanup(dirA);
                TestFs.Cleanup(dirB);
            }
        });

        Register("e2e: каждая версия пишет код на своём диалекте Морфика", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                kernel.GenerateSources(6);

                var sources = new List<string>();
                for (int v = 2; v <= 7; v++)
                {
                    sources.Add(File.ReadAllText(Path.Combine(dir, "source", $"v{v:D4}.morphik")));
                }

                int distinct = sources.Distinct().Count();
                Equal(6, distinct, "исходники версий не различаются");
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });

        Register("e2e: окна открываются и сцены рисуются без исключений", () =>
        {
            string dir = TestFs.NewTempDir();
            try
            {
                using var kernel = new Scp603Kernel(Options(dir));
                var windows = kernel.GeneratePrimes(400).Where(x => x.Window is not null).Select(x => x.Window!.Value).ToList();
                True(windows.Count >= 5, "недостаточно окон для проверки");

                var runner = new HeadlessSceneRunner();
                foreach (WindowEvent w in windows)
                {
                    IScene scene = runner.Open(w.SceneId, kernel.Profile, (ulong)w.Prime);
                    for (int i = 0; i < 3; i++)
                    {
                        scene.Update(0.1);
                        NotEmpty(scene.ToText());
                    }

                    runner.Close(scene);
                }

                Equal(0, runner.Active.Count);
            }
            finally
            {
                TestFs.Cleanup(dir);
            }
        });
    }
}
