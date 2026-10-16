using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-118 — "searching for a printer" followed by a document appearing. This simulation
/// never talks to a real printer: the document is written as a text file into the simulator's
/// own output folder through a host side effect.
/// </summary>
public sealed class PrinterScene : SceneBase, ISceneEffects
{
    public const string Document =
        "SCP FOUNDATION — СЛУЖЕБНАЯ ЗАПИСКА (реконструкция)\n" +
        "───────────────────────────────────────────────\n" +
        "Кому : Д-р Браун\n" +
        "От   : Отдел вычислительных аномалий, каб. 12-А\n" +
        "Тема : SCP-603-118 — вывод на печать\n\n" +
        "При запуске сцена ищет печатающее устройство и, найдя его, выводит данный\n" +
        "документ. На момент запуска во внутренней сети числилось 203 принтера.\n\n" +
        "Настоящая имитация не обращается ни к каким устройствам: файл сохранён\n" +
        "в локальном каталоге вывода симулятора. Автор записи: д-р ██████.\n";

    private readonly List<SceneEffect> _effects = new();
    private readonly List<string> _log = new();
    private double _timer;
    private int _searches;
    private bool _printed;

    public int Searches => _searches;

    public bool Printed => _printed;

    public override string Id => "SCP-603-118";

    public override string Name => "SCP-603-118";

    public override string Title => "Поиск принтера";

    public override SceneKind Kind => SceneKind.Notification;

    /// <summary>Canonically the program prints "Поиск изображения"/search progress about every 20 s.</summary>
    public double SearchStepSeconds { get; set; } = 20.0;

    /// <summary>Initial line plus five repeated searches gives roughly the documented two minutes.</summary>
    public int SearchesBeforePrint { get; set; } = 6;

    protected override void OnStart()
    {
        _log.Clear();
        _log.Add("Выполняется поиск принтера...");
        _timer = 0;
        _searches = 0;
        _printed = false;
    }

    public override void Update(double deltaSeconds)
    {
        if (_printed || Status != SceneStatus.Running)
        {
            return;
        }

        _timer += deltaSeconds;
        if (_timer < SearchStepSeconds)
        {
            return;
        }

        _timer = 0;
        _searches++;
        _log.Add("Поиск...");

        if (_searches >= SearchesBeforePrint)
        {
            _printed = true;
            _log.Add("Устройство найдено. Вывод документа...");
            _effects.Add(new SceneEffect("print", Document));
            Status = SceneStatus.Finished;
        }
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);
        int y = 30;
        canvas.MeasureText("Hg", 13, false, out _, out int lh);
        foreach (string line in _log)
        {
            canvas.DrawText(20, y, line, line.StartsWith("Устройство", StringComparison.Ordinal) ? Rgba.Green : Rgba.Gray(210), 13);
            y += lh;
        }

        canvas.DrawText(10, 6, "SCP-603-118 · документ выводится в каталог симулятора", Rgba.Gray(140), 11);
    }

    public IReadOnlyList<SceneEffect> DrainEffects()
    {
        var copy = _effects.ToArray();
        _effects.Clear();
        return copy;
    }
}
