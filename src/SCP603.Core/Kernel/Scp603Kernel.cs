using Scp603.Core.Diagnostics;
using Scp603.Core.Morphik;
using Scp603.Core.Numerics;
using Scp603.Core.Scenes;
using Scp603.Core.Versioning;

namespace Scp603.Core.Kernel;

/// <summary>
/// The heart of the simulator. It owns two cooperating threads, mirroring the object's
/// description: a main thread that emits primes, and a second thread that keeps mutating the
/// stored source of the program ("Morphik"). Everything is deterministic and safe.
/// </summary>
public sealed class Scp603Kernel : IDisposable
{
    private readonly KernelOptions _options;
    private readonly Journal _journal;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _primeTask;
    private Task? _forgeTask;
    private int _windowsOpened;

    public Scp603Kernel(KernelOptions? options = null)
    {
        _options = options ?? new KernelOptions();
        _journal = new Journal(_options.JournalToFile ? _options.JournalPath : null);
        _journal.Written += line => Log?.Invoke(line);

        Directory.CreateDirectory(_options.SourceDirectory);
        Directory.CreateDirectory(_options.OutputDirectory);

        Profile = MorphikProfileFactory.ForVersion(_options.StartVersion);
        SceneSet = SceneSet.Build(Profile);
        LatestVersion = Profile.Version;
    }

    public KernelOptions Options => _options;

    public Journal Journal => _journal;

    public MorphikProfile Profile { get; private set; }

    public SceneSet SceneSet { get; private set; }

    public int LatestVersion { get; private set; }

    public int WindowsOpened => Volatile.Read(ref _windowsOpened);

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public event Action<PrimeEvent>? PrimeGenerated;

    public event Action<WindowEvent>? WindowOpened;

    public event Action<VersionEvent>? VersionEmitted;

    public event Action<string>? Log;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        _journal.Write($"SCP-603 v{Profile.Version} запущена. {Profile.Describe()}");

        if (!_options.SourceOnly)
        {
            _primeTask = Task.Run(() => PrimeLoop(token), token);
        }

        if (_options.EnableSourceForge)
        {
            _forgeTask = Task.Run(() => ForgeLoop(token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts = _cts;
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        try
        {
            Task.WaitAll(new[] { _primeTask, _forgeTask }.Where(t => t is not null).ToArray()!, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _journal.Write("Ошибка остановки: " + ex.Message);
        }

        cts.Dispose();
        _cts = null;
        _journal.Write("SCP-603 остановлена (процесс завершён средствами ОС).");
    }

    /// <summary>Synchronously generates the requested number of primes, returning the events.</summary>
    public IReadOnlyList<(PrimeEvent Prime, WindowEvent? Window)> GeneratePrimes(int count)
    {
        var results = new List<(PrimeEvent, WindowEvent?)>();
        MorphikProfile profile = Profile;
        long value = profile.StartValue;
        int index = profile.IndexOneBased ? 1 : 0;
        long seq = 0;
        int windows = 0;

        while (results.Count < count)
        {
            if (Primes.IsPrime(value))
            {
                seq++;
                var prime = new PrimeEvent(value, index, MorphikToolchain.FormatValue(value, profile), seq);
                WindowEvent? window = null;
                if (Primes.IsPrime(index))
                {
                    windows++;
                    string sceneId = SceneSet.SceneForWindow(windows);
                    window = new WindowEvent(value, windows, sceneId, SceneCatalog.NameOf(sceneId));
                }

                results.Add((prime, window));
                index++;
            }

            if (value >= long.MaxValue - 2)
            {
                break;
            }

            value++;
        }

        return results;
    }

    /// <summary>Regenerates source for <paramref name="count"/> new versions without threads.</summary>
    public IReadOnlyList<VersionEvent> GenerateSources(int count)
    {
        var emitted = new List<VersionEvent>();
        for (int i = 0; i < count; i++)
        {
            emitted.Add(ProduceNextVersion());
        }

        return emitted;
    }

    public MegaprimeArtifact CompileFiles(IEnumerable<string> files, string? outputPath = null)
    {
        var statements = new List<MorphikStmt>();
        MorphikProfile? lastProfile = null;
        var names = new List<string>();

        foreach (string file in files)
        {
            MorphikProfile profile = ProfileForFile(file);
            string source = File.ReadAllText(file);
            ProgramNode program = Parser.Parse(profile, source);
            statements.AddRange(program.Statements);
            lastProfile = profile;
            names.Add(Path.GetFileName(file));
            _journal.Write($"Скомпилирован {Path.GetFileName(file)} (версия Морфика {profile.Version}).");
        }

        MorphikProfile use = lastProfile ?? Profile;
        var linked = new ProgramNode(statements.ToArray());
        MegaprimeArtifact artifact = MorphikToolchain.BuildArtifact(use, linked);

        outputPath ??= Path.Combine(_options.OutputDirectory, "megaprime");
        artifact.Save(outputPath);
        File.WriteAllText(
            outputPath + ".manifest.txt",
            $"megaprime\nверсия морфика : {use.Version}\nдайджест       : {use.Digest():x16}\n" +
            $"файлов         : {names.Count}\nисточники      : {string.Join(", ", names)}\n");
        _journal.Write($"Слинковано {names.Count} файл(ов) -> {outputPath}");
        return artifact;
    }

    public MorphikProfile ProfileForFile(string file)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        if (name.StartsWith('v') && int.TryParse(name.AsSpan(1), out int version) && version >= 1)
        {
            return MorphikProfileFactory.ForVersion(version);
        }

        return Profile;
    }

    /// <summary>Handles a scene side effect by writing into the simulator's own output folder.</summary>
    public void ApplyEffect(SceneEffect effect)
    {
        Directory.CreateDirectory(_options.OutputDirectory);
        switch (effect.Kind)
        {
            case "print":
                {
                    string path = Path.Combine(_options.OutputDirectory, "printout.txt");
                    File.WriteAllText(path, effect.Payload);
                    _journal.Write($"SCP-603-118: документ сохранён в {path} (реальный принтер не задействован).");
                    break;
                }

            case "narrative":
                {
                    string path = Path.Combine(_options.OutputDirectory, "narrative.txt");
                    File.WriteAllText(path, effect.Payload);
                    _journal.Write($"SCP-603-121: повествование сохранено в {path}.");
                    break;
                }

            default:
                _journal.Write($"[{effect.Kind}] {effect.Payload}");
                break;
        }
    }

    private void PrimeLoop(CancellationToken token)
    {
        MorphikProfile profile = Profile; // frozen for the lifetime of this running instance
        long value = profile.StartValue;
        int index = profile.IndexOneBased ? 1 : 0;
        long seq = 0;

        int consecutiveErrors = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (Primes.IsPrime(value))
                {
                    seq++;
                    PrimeGenerated?.Invoke(new PrimeEvent(
                        value, index, MorphikToolchain.FormatValue(value, profile), seq));

                    if (Primes.IsPrime(index))
                    {
                        int windows = Interlocked.Increment(ref _windowsOpened);
                        string sceneId = SceneSet.SceneForWindow(windows);
                        WindowOpened?.Invoke(new WindowEvent(
                            value, windows, sceneId, SceneCatalog.NameOf(sceneId)));
                    }

                    index++;
                }

                consecutiveErrors = 0;

                if (value >= long.MaxValue - 2)
                {
                    break;
                }

                value++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A subscriber or a scene failed us: log it and keep the main thread alive,
                // skipping the offending value so we never spin on the same failure.
                consecutiveErrors++;
                _journal.Write($"Основной поток: сбой шага ({ex.Message}), продолжаю.");
                if (value < long.MaxValue - 2)
                {
                    value++;
                }

                if (consecutiveErrors > 1000)
                {
                    _journal.Write("Основной поток: слишком много подряд сбоев, поток остановлен.");
                    break;
                }
            }

            if (_options.PrimeDelayMs > 0 && token.WaitHandle.WaitOne(_options.PrimeDelayMs))
            {
                break;
            }
        }
    }

    private void ForgeLoop(CancellationToken token)
    {
        for (int i = 0; i < _options.ForgeTargetVersions && !token.IsCancellationRequested; i++)
        {
            try
            {
                if (_options.ForgeStepDelayMs > 0 && token.WaitHandle.WaitOne(_options.ForgeStepDelayMs))
                {
                    break;
                }

                ProduceNextVersion();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed generation step must never take down the whole object:
                // log it and try the next step (the source file simply is not written).
                _journal.Write($"Кузница: шаг генерации не удался ({ex.Message}), пробую снова.");
            }
        }
    }

    private VersionEvent ProduceNextVersion()
    {
        MorphikProfile current;
        lock (_gate)
        {
            current = Profile;
        }

        int nextVersion = current.Version + 1;
        var rng = new XorShift64(unchecked(0x5C603UL ^ ((ulong)nextVersion * 0x9E3779B97F4A7C15UL)));
        MorphikProfile next = MorphikProfileFactory.Mutate(current, rng);

        ProgramNode program = ProgramTemplate.Build(next);
        ProgramNode obfuscated = MorphikObfuscator.Obfuscate(program, new XorShift64(unchecked(next.Digest() ^ 0xA5A5A5A5UL)));
        string source = CodeGenerator.Generate(next, obfuscated);

        string header =
            $"{next.CommentPrefix} SCP-603 — собственная копия исходного кода, версия {next.Version}\n" +
            $"{next.CommentPrefix} язык: {next.LanguageName}, дайджест: {next.Digest():x16}\n";
        string path = Path.Combine(_options.SourceDirectory, $"v{next.Version:D4}.morphik");
        File.WriteAllText(path, header + source);

        lock (_gate)
        {
            Profile = next;
            SceneSet = SceneSet.Build(next);
            LatestVersion = next.Version;
        }

        var evt = new VersionEvent(next.Version, path, next.Digest());
        _journal.Write($"SCP-603: безошибочный цикл завершён, версия {next.Version} выведена в {path}.");
        VersionEmitted?.Invoke(evt);
        return evt;
    }

    public void Dispose()
    {
        Stop();
        _journal.Dispose();
    }
}
