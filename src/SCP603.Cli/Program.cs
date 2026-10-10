using System.Text;
using Scp603.Core.Kernel;
using Scp603.Core.Morphik;
using Scp603.Core.Rendering;
using Scp603.Core.Scenes;

namespace Scp603.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        var parsed = ParsedArgs.Parse(args);

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "run" => Run(parsed),
                "versions" => Versions(parsed.PositionalInt(1, 5), OptionsFrom(parsed)),
                "compile" => Compile(parsed.Positional.ToArray(), OptionsFrom(parsed)),
                "scene" => Scene(parsed.PositionalString(1, SceneCatalog.DieId), OptionsFrom(parsed)),
                "info" => Info(parsed.FlagInt("--version", 1)),
                "list-scenes" => ListScenes(),
                "selftest" => SelfTest(),
                _ => Unknown(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ошибка: " + ex.Message);
            return 2;
        }
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Неизвестная команда «{command}». Выполните «scp603 help».");
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            "SCP-603 — симулятор (безопасная и этичная имитация)\n" +
            "\n" +
            "Использование:\n" +
            "  scp603 run [--count N] [--delay MS] [--version V] [--base DIR]\n" +
            "        Основной поток: генерация простых чисел и открытие окон.\n" +
            "  scp603 versions [N]\n" +
            "        Второй поток: вывести N новых версий исходного кода в source/.\n" +
            "  scp603 compile <файл.morphik> [...]\n" +
            "        Скомпилировать и слинковать файлы в артефакт megaprime.\n" +
            "  scp603 scene <id> [--version V]\n" +
            "        Запустить одну сцену без графики (для проверки).\n" +
            "  scp603 info [--version V]\n" +
            "        Показать параметры диалекта Морфика и набор сцен версии.\n" +
            "  scp603 list-scenes\n" +
            "        Список всех задокументированных сцен.\n" +
            "  scp603 selftest\n" +
            "        Быстрый прогон внутренних инвариантов.\n");
    }

    private static KernelOptions OptionsFrom(ParsedArgs parsed) => new()
    {
        BaseDirectory = parsed.Flag("--base", Directory.GetCurrentDirectory()),
        StartVersion = parsed.FlagInt("--version", 1),
        PrimeDelayMs = parsed.FlagInt("--delay", 0),
        JournalToFile = true,
        ForgeTargetVersions = 0,
        EnableSourceForge = false,
    };

    private static int Run(ParsedArgs parsed)
    {
        KernelOptions options = OptionsFrom(parsed);
        int count = parsed.FlagInt("--count", 40);
        using var kernel = new Scp603Kernel(options);
        Console.WriteLine($"SCP-603 v{kernel.Profile.Version}. {kernel.Profile.Describe()}");
        Console.WriteLine(new string('-', 60));

        IReadOnlyList<(PrimeEvent Prime, WindowEvent? Window)> results = kernel.GeneratePrimes(count);
        foreach ((PrimeEvent prime, WindowEvent? window) in results)
        {
            if (window is not null)
            {
                Console.WriteLine();
                Console.WriteLine($"  >>> окно №{window.Value.WindowNumber} на простом числе {window.Value.Prime}: " +
                                  $"{window.Value.SceneName} [{window.Value.SceneId}]");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"  {prime.SequenceNumber,8}  {prime.Formatted}");
            }
        }

        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"Сгенерировано простых чисел: {results.Count}, окон: {results.Count(r => r.Window is not null)}.");
        return 0;
    }

    private static int Versions(int count, KernelOptions options)
    {
        options.EnableSourceForge = true;
        options.ForgeTargetVersions = count;
        options.ForgeStepDelayMs = 0;
        using var kernel = new Scp603Kernel(options);
        Console.WriteLine($"Базовый каталог: {kernel.Options.SourceDirectory}");

        IReadOnlyList<VersionEvent> versions = kernel.GenerateSources(count);
        foreach (VersionEvent v in versions)
        {
            MorphikProfile p = MorphikProfileFactory.ForVersion(v.Version);
            Console.WriteLine($"  v{v.Version,-4} дайджест {v.Digest:x16}  {p.Describe()}");
            Console.WriteLine($"        {v.SourcePath}");
        }

        Console.WriteLine($"Выведено версий: {versions.Count}.");
        return 0;
    }

    private static int Compile(string[] files, KernelOptions options)
    {
        if (files.Length == 0)
        {
            Console.Error.WriteLine("Укажите хотя бы один файл .morphik для компиляции.");
            return 2;
        }

        using var kernel = new Scp603Kernel(options);
        MegaprimeArtifact artifact = kernel.CompileFiles(files);
        string path = Path.Combine(options.OutputDirectory, "megaprime");
        Console.WriteLine($"megaprime: версия морфика {artifact.ProfileVersion}, дайджест {artifact.ProfileDigest:x16}");
        Console.WriteLine($"Инструкций: {artifact.Bytecode.Instructions.Length}");
        Console.WriteLine($"Файл: {path}");

        var host = new RecordingMorphikHost { StopAfterEmits = 20 };
        new MorphikVm { StepLimit = 2_000_000 }.Run(artifact.Bytecode, host);
        Console.WriteLine("Пробный запуск: " + string.Join(" ", host.Emitted.Select(Convert.ToInt64).Take(20)));
        return 0;
    }

    private static int Scene(string id, KernelOptions options)
    {
        using var kernel = new Scp603Kernel(options);
        var runner = new HeadlessSceneRunner(options.Scenes);
        runner.EffectProduced += effect =>
        {
            kernel.ApplyEffect(effect);
            Console.WriteLine($"[эффект] {effect.Kind}");
        };

        IScene scene = runner.Open(id, kernel.Profile, 0xC0FFEE);
        Console.WriteLine($"Сцена {scene.Name} [{scene.Id}]. Управление: {DescribeControls(id)}");
        Console.WriteLine(new string('-', 60));

        while (scene.Status is SceneStatus.Running or SceneStatus.NotStarted)
        {
            RenderScene(scene);
            Console.Write("> ");
            string? line = Console.ReadLine();
            if (line is null)
            {
                break;
            }

            if (line == ":q")
            {
                break;
            }

            if (line.Length == 0)
            {
                scene.OnInput(SceneInput.Text_("\n"));
            }
            else if (line == ":enter")
            {
                scene.OnInput(SceneInput.Key_("Enter"));
            }
            else
            {
                scene.OnInput(SceneInput.Text_(line));
            }

            runner.Update(scene, 0.25);
        }

        RenderScene(scene);
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"Состояние сцены: {scene.Status}.");
        return 0;
    }

    private static string DescribeControls(string id) => id switch
    {
        SceneCatalog.DieId => "введите команду; пустая строка повторяется; «:q» — выход.",
        SceneCatalog.JacobId => "введите фразу; «:q» — выход.",
        SceneCatalog.Jacob508Id => "«:enter» — нажать единственную работающую кнопку; «:q» — выход.",
        _ => "«:enter» — событие Enter; «:q» — выход.",
    };

    private static void RenderScene(IScene scene)
    {
        var canvas = new TextCanvas(80 * 8, 24 * 16, 8, 16);
        canvas.Clear(Rgba.Black);
        scene.Draw(canvas);
        Console.WriteLine(canvas.Render());
    }

    private static int Info(int version)
    {
        MorphikProfile profile = MorphikProfileFactory.ForVersion(version);
        Console.WriteLine($"Версия объекта : {profile.Version}");
        Console.WriteLine($"Язык           : {profile.Describe()}");
        Console.WriteLine($"Дайджест       : {profile.Digest():x16}");
        Console.WriteLine($"Заголовок окна : {profile.WindowTitle}");
        Console.WriteLine($"Терминатор     : {profile.Terminator} {(profile.NewlineTerminates ? "(конец строки)" : "")}");
        Console.WriteLine($"Сцены версии   : {string.Join(", ", Core.Versioning.SceneSet.Build(profile).Sequence)}");

        ProgramNode program = ProgramTemplate.Build(profile);
        string source = CodeGenerator.Generate(profile, program);
        string[] lines = source.Split('\n');
        Console.WriteLine();
        Console.WriteLine($"--- пример кода (первые 12 из {lines.Length} строк) ---");
        foreach (string line in lines.Take(12))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    private static int ListScenes()
    {
        foreach (string id in SceneCatalog.AllIds)
        {
            IScene scene = SceneCatalog.Create(id);
            Console.WriteLine($"{id,-14} {scene.Kind,-14} {scene.Name}");
        }

        return 0;
    }

    private static int SelfTest()
    {
        // A tiny invariant check so the CLI itself can prove it is working.
        int failures = 0;
        for (int v = 1; v <= 30; v++)
        {
            MorphikProfile profile = MorphikProfileFactory.ForVersion(v);
            ProgramNode program = ProgramTemplate.Build(profile);
            string source = CodeGenerator.Generate(profile, program);
            ProgramNode reparsed = Parser.Parse(profile, source);
            if (!AstComparer.ProgramEquals(program, reparsed))
            {
                Console.WriteLine($"  ПРОВАЛ: v{v} не воспроизводится");
                failures++;
            }
        }

        Console.WriteLine(failures == 0
            ? "Самопроверка CLI: всё в порядке (версии 1..30 воспроизводятся)."
            : $"Самопроверка CLI: провалов {failures}.");
        return failures == 0 ? 0 : 1;
    }

}

/// <summary>
/// Minimal argument parser: separates <c>--flag value</c> / <c>--flag=value</c> pairs from
/// positional arguments, so that flag values are never mistaken for file names.
/// </summary>
public sealed class ParsedArgs
{
    private readonly Dictionary<string, string> _flags = new(StringComparer.Ordinal);
    private readonly List<string> _positional = new();

    public IReadOnlyList<string> Positional => _positional;

    public static ParsedArgs Parse(string[] args)
    {
        var parsed = new ParsedArgs();
        for (int i = 1; i < args.Length; i++)
        {
            string token = args[i];
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                int eq = token.IndexOf('=');
                if (eq > 0)
                {
                    parsed._flags[token[..eq]] = token[(eq + 1)..];
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    parsed._flags[token] = args[++i];
                }
                else
                {
                    parsed._flags[token] = string.Empty;
                }
            }
            else
            {
                parsed._positional.Add(token);
            }
        }

        return parsed;
    }

    public string Flag(string name, string fallback)
        => _flags.TryGetValue(name, out string? value) && value.Length > 0 ? value : fallback;

    public int FlagInt(string name, int fallback)
        => _flags.TryGetValue(name, out string? value) && int.TryParse(value, out int v) ? v : fallback;

    public int PositionalInt(int index, int fallback)
        => index < _positional.Count && int.TryParse(_positional[index], out int v) ? v : fallback;

    public string PositionalString(int index, string fallback)
        => index < _positional.Count ? _positional[index] : fallback;
}
