using Scp603.Core.Numerics;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-121 "Сказитель" — finds the largest suitable image and re-renders it from tiny
/// glyphs. The record claims a memetic "inspiration" effect; here it only prints a story,
/// and the file search is deliberately restricted to folders the operator allows.
/// </summary>
public sealed class TellerScene : SceneBase, ISceneEffects
{
    private const string Ramp = " .·:;~+=*#%@ЖΩΞΨᚠᚢᚦ█";

    private enum Phase
    {
        Searching,
        Processing,
        Done,
    }

    private readonly List<SceneEffect> _effects = new();
    private readonly List<string> _log = new();
    private readonly IReadOnlyList<string> _searchDirs;
    private readonly bool _broadScan;
    private XorShift64 _rng = new(1);
    private Phase _phase;
    private double _timer;
    private GrayImage? _image;
    private string _source = "нет";
    private string _narrative = string.Empty;

    public TellerScene(IReadOnlyList<string>? searchDirs = null, bool broadScan = false)
    {
        _searchDirs = searchDirs ?? Array.Empty<string>();
        _broadScan = broadScan;
    }

    public string Narrative => _narrative;

    public string ImageSource => _source;

    public override string Id => "SCP-603-121";

    public override string Name => "Сказитель";

    public override string Title => "Поиск изображения";

    public override SceneKind Kind => SceneKind.Visual;

    public double SearchSeconds { get; set; } = 3.0;

    public double ProcessingSeconds { get; set; } = 2.0;

    protected override void OnStart()
    {
        _rng = new XorShift64(Seed);
        _log.Clear();
        _log.Add("Поиск изображения");
        _phase = Phase.Searching;
        _timer = 0;
        _image = null;
        _source = "нет";
        _narrative = string.Empty;
    }

    public override void Update(double deltaSeconds)
    {
        if (Status != SceneStatus.Running)
        {
            return;
        }

        _timer += deltaSeconds;
        switch (_phase)
        {
            case Phase.Searching:
                if (_timer >= SearchSeconds)
                {
                    _timer = 0;
                    _image = FindImage();
                    _log.Add("Изображение найдено, обработка...");
                    _phase = Phase.Processing;
                }

                break;

            case Phase.Processing:
                if (_timer >= ProcessingSeconds)
                {
                    _timer = 0;
                    _phase = Phase.Done;
                    _narrative = ComposeNarrative(_image!);
                    _effects.Add(new SceneEffect("narrative", _narrative));
                    Status = SceneStatus.Finished;
                }

                break;
        }
    }

    private GrayImage FindImage()
    {
        GrayImage? best = null;
        long bestPixels = -1;
        int examined = 0;
        const int maxFiles = 20000;

        IEnumerable<string> roots = _searchDirs;
        if (_broadScan)
        {
            roots = _searchDirs
                .Concat(DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.RootDirectory.FullName));
        }

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (string file in files)
            {
                if (examined++ > maxFiles)
                {
                    break;
                }

                if (!GrayImage.IsSupported(file))
                {
                    continue;
                }

                GrayImage? img = GrayImage.TryLoad(file);
                if (img is null)
                {
                    continue;
                }

                if (img.Width < 1000 || img.Height < 1000)
                {
                    continue;
                }

                double ratio = img.AspectRatio;
                if (ratio < 0.6 || ratio > 1.9)
                {
                    continue;
                }

                long pixels = (long)img.Width * img.Height;
                if (pixels > bestPixels)
                {
                    bestPixels = pixels;
                    best = img;
                    _source = file;
                }
            }
        }

        if (best is null)
        {
            best = GrayImage.Procedural(1024, 768, Seed);
            _source = "сгенерировано (подходящих файлов не найдено)";
        }

        return best;
    }

    private string ComposeNarrative(GrayImage image)
    {
        ulong h = image.ContentHash() ^ Seed;
        var open = new[]
        {
            "Она стояла на пороге дома, где прошло её детство.",
            "Ветер нёс запах моря, и он вдруг вспомнил, как это — быть ребёнком.",
            "Свет падал так, как падает только в конце долгого дня.",
            "Никто не сказал ни слова, но все всё поняли.",
        };
        var mid = new[]
        {
            "Между ними лежала целая жизнь, и всё же они узнали друг друга сразу.",
            "Старые часы на стене шли, отсчитывая то, чего уже не вернуть.",
            "Он думал о письме, которое так и не отправил.",
            "Дом помнил больше, чем любой из них.",
        };
        var close = new[]
        {
            "И тогда она улыбнулась — впервые за много лет.",
            "История заканчивается здесь, но не заканчивается сам свет.",
            "Они ушли вместе, и это было достаточно.",
            "Кто-то выключил свет, и всё стало на свои места.",
        };

        string a = open[(int)(h % (ulong)open.Length)];
        string b = mid[(int)((h >> 8) % (ulong)mid.Length)];
        string c = close[(int)((h >> 16) % (ulong)close.Length)];
        return $"{a} {b} {c}";
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);
        canvas.DrawText(10, 6, "SCP-603-121 «Сказитель» · СИМУЛЯЦИЯ (внушение не производится)", Rgba.FromArgb(255, 200, 200, 160), 11, true);

        switch (_phase)
        {
            case Phase.Searching:
                canvas.DrawText(20, 40, "Поиск изображения", Rgba.White, 16, true);
                canvas.DrawText(20, 70, "Область поиска: " + (_searchDirs.Count == 0 ? "(не задана)" : string.Join("; ", _searchDirs)), Rgba.Gray(150), 11);
                break;

            case Phase.Processing:
                {
                    canvas.DrawText(20, 40, "Изображение найдено, обработка...", Rgba.White, 16, true);
                    double p = Math.Clamp(_timer / ProcessingSeconds, 0, 1);
                    canvas.FillRect(20, 80, (int)((canvas.Width - 40) * p), 12, Rgba.FromArgb(255, 90, 140, 220));
                    break;
                }

            case Phase.Done when _image is not null:
                RenderGlyphImage(canvas, _image, 20, 30, canvas.Width - 40, canvas.Height - 90);
                canvas.DrawText(20, canvas.Height - 50, _narrative, Rgba.Gray(220), 11);
                canvas.DrawText(20, canvas.Height - 30, "Источник: " + Path.GetFileName(_source), Rgba.Gray(130), 10);
                break;
        }
    }

    private static void RenderGlyphImage(ICanvas canvas, GrayImage image, int x, int y, int w, int h)
    {
        int cols = 96;
        int rows = 34;
        double sx = (double)image.Width / cols;
        double sy = (double)image.Height / rows;
        for (int r = 0; r < rows; r++)
        {
            var sb = new System.Text.StringBuilder(cols);
            for (int c = 0; c < cols; c++)
            {
                int px = (int)(c * sx);
                int py = (int)(r * sy);
                byte v = image.At(Math.Min(px, image.Width - 1), Math.Min(py, image.Height - 1));
                int idx = v * (Ramp.Length - 1) / 255;
                sb.Append(Ramp[idx]);
            }

            canvas.DrawText(x, y + (r * (h / rows)), sb.ToString(), Rgba.Gray(210), 8);
        }
    }

    public IReadOnlyList<SceneEffect> DrainEffects()
    {
        var copy = _effects.ToArray();
        _effects.Clear();
        return copy;
    }
}
