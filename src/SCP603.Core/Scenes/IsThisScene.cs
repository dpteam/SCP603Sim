using Scp603.Core.Numerics;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-734 "Является ли это" — procedurally drawn 320x240 style images with a yes/no
/// question. The sequence never repeats and the "control" images that end the process are
/// represented here as a graceful simulated failure, never a real one.
/// </summary>
public sealed class IsThisScene : SceneBase
{
    private sealed record Item(string Label, bool IsPresent, int Shape, bool IsControl);

    private static readonly Item[] Items =
    {
        new("яблоко", true, 0, false),
        new("кот", false, 1, false),
        new("дом", true, 2, false),
        new("дерево", false, 3, false),
        new("луна", true, 0, false),
        new("рыба", false, 4, false),
        new("книга", true, 1, false),
        new("стул", true, 2, false),
        new("собака", false, 4, false),
        new("ключ", true, 3, false),
        new("контроль A", true, 0, true),
        new("контроль B", false, 1, true),
    };

    private readonly LineInput _input = new(8);
    private XorShift64 _rng = new(1);
    private int _index;
    private string _feedback = "Введите ДА или НЕТ";
    private bool _failed;

    public int Index => _index;

    public bool Failed => _failed;

    public override string Id => "SCP-603-734";

    public override string Name => "Является ли это";

    public override string Title => "Является ли это";

    public override SceneKind Kind => SceneKind.Interactive;

    protected override void OnStart()
    {
        _rng = new XorShift64(Seed);
        _index = 0;
        _feedback = "Введите ДА или НЕТ";
        _failed = false;
    }

    public override void OnInput(in SceneInput input) => _input.Feed(input, Submit);

    public void Submit(string text)
    {
        if (_failed || Status != SceneStatus.Running)
        {
            return;
        }

        string answer = text.Trim().ToLowerInvariant();
        if (answer is not ("да" or "нет" or "yes" or "no" or "y" or "n"))
        {
            _feedback = "Нужно ввести ДА или НЕТ";
            return;
        }

        bool saidYes = answer is "да" or "yes" or "y";
        Item item = Items[_index];
        bool correct = saidYes == item.IsPresent;

        if (item.IsControl && !correct)
        {
            _failed = true;
            Status = SceneStatus.Crashed;
            _feedback = "НЕОЖИДАННЫЙ КОНЕЦ ФАЙЛА";
            return;
        }

        _index++;
        if (_index >= Items.Length)
        {
            Status = SceneStatus.Finished;
            _feedback = "Последовательность завершена";
            return;
        }

        _feedback = correct ? "Принято" : "Ответ не совпал (контроль не задет)";
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);

        // Simulated 320x240 viewport.
        int vw = 320, vh = 240;
        int vx = (canvas.Width - vw) / 2;
        int vy = 30;
        canvas.FillRect(vx, vy, vw, vh, Rgba.FromArgb(255, 8, 8, 20));
        canvas.DrawRect(vx, vy, vw, vh, Rgba.Gray(120));

        if (!_failed && _index < Items.Length)
        {
            Item item = Items[_index];
            DrawShape(canvas, vx, vy, vw, vh, item.Shape, item.IsPresent);
            string q = $"Это {item.Label}?";
            canvas.MeasureText(q, 14, true, out int qw, out _);
            canvas.DrawText(vx + (vw - qw) / 2, vy + vh - 26, q, Rgba.White, 14, true);
        }
        else
        {
            canvas.MeasureText("НЕОЖИДАННЫЙ КОНЕЦ ФАЙЛА", 14, true, out int fw, out _);
            canvas.DrawText(vx + (vw - fw) / 2, vy + (vh / 2), "НЕОЖИДАННЫЙ КОНЕЦ ФАЙЛА", Rgba.FromArgb(255, 220, 80, 80), 14, true);
        }

        canvas.DrawText(10, 6, "SCP-603-734 · 320x240 · последовательность неизменна", Rgba.Gray(140), 11);
        canvas.DrawText(vx, vy + vh + 10, "» " + _input.Text, Rgba.Green, 13, true);
        canvas.DrawText(vx, vy + vh + 30, _feedback, Rgba.Gray(200), 11);
        canvas.DrawText(vx, vy + vh + 50, $"Кадр {Math.Min(_index + 1, Items.Length)} из {Items.Length}", Rgba.Gray(120), 11);
    }

    private void DrawShape(ICanvas canvas, int vx, int vy, int vw, int vh, int shape, bool present)
    {
        int cx = vx + (vw / 2);
        int cy = vy + (vh / 2) - 10;
        int s = 50;
        uint color = Rgba.FromArgb(255, 180, 200, 255);

        // A "present" image always shows the object; missing images show only a frame,
        // which is exactly what the yes/no question is about.
        if (!present)
        {
            canvas.DrawRect(cx - s - 16, cy - s - 16, (s + 16) * 2, (s + 16) * 2, Rgba.Gray(90));
            var rng = new XorShift64(Seed ^ (ulong)(_index + 1));
            canvas.DrawText(cx - s, cy, "…" + (char)('а' + rng.NextInt(0, 32)), Rgba.Gray(70), 20);
            return;
        }

        switch (shape)
        {
            case 0:
                canvas.FillCircle(cx, cy, s, color);
                break;
            case 1:
                canvas.FillRect(cx - s, cy - s, s * 2, s * 2, color);
                break;
            case 2:
                canvas.DrawTriangle(cx, cy - s, cx + s, cy + s, cx - s, cy + s, color, true);
                break;
            case 3:
                canvas.FillRect(cx - 12, cy - s, 24, s * 2, color);
                canvas.FillRect(cx - s, cy - 12, s * 2, 24, color);
                break;
            default:
                canvas.FillPolygon(new[]
                {
                    (cx, cy - s), (cx + s / 3, cy - s / 3), (cx + s, cy),
                    (cx + s / 3, cy + s / 3), (cx, cy + s), (cx - s / 3, cy + s / 3),
                    (cx - s, cy), (cx - s / 3, cy - s / 3),
                }, color);
                break;
        }
    }
}
