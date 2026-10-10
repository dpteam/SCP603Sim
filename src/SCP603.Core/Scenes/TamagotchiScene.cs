using Scp603.Core.Numerics;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-70 "Тамагочи" — a crudely drawn face and three buttons. In the record the buttons
/// act on Dr. Giles' body; here they only produce narrative lines about the fictional doctor.
/// Nothing is transmitted anywhere.
/// </summary>
public sealed class TamagotchiScene : SceneBase, ISceneEffects
{
    public static readonly string[] Moods =
    {
        "Скучающий", "Довольный", "Голодный", "Испытывает жажду", "Уставший", "Энергичный",
        "Печальный", "Возбуждённый", "Переутомлённый", "Влюблённый", "Безразличный", "В туалете",
    };

    private readonly List<SceneEffect> _effects = new();
    private readonly List<string> _log = new();
    private XorShift64 _rng = new(1);
    private int _mood;

    public string CurrentMood => Moods[_mood];

    public override string Id => "SCP-603-70";

    public override string Name => "Тамагочи";

    public override string Title => "Тамагочи";

    public override SceneKind Kind => SceneKind.Interactive;

    protected override void OnStart()
    {
        _rng = new XorShift64(Seed);
        _mood = _rng.NextInt(0, Moods.Length);
        _log.Clear();
        _log.Add("Наблюдение начато. Д-р Джайлс (модель) подключён к сцене.");
    }

    public void PressButton(int button)
    {
        string mood = CurrentMood;
        string effect = Describe(mood, button);
        _log.Add($"[{mood}] кнопка {button}: {effect}");
        _effects.Add(new SceneEffect("tamagotchi", $"{mood}|{button}|{effect}"));
        _mood = _rng.NextInt(0, Moods.Length);
    }

    private static string Describe(string mood, int button) => (mood, button) switch
    {
        ("Голодный", 1) => "Д-р Джайлс испытает сильное желание отправиться на поиски еды.",
        ("Голодный", 2) => "Утоляет чувство голода у д-ра Джайлса на срок до одного часа.",
        ("Голодный", 3) => "Д-ра Джайлса немедленно стошнит.",
        ("Уставший", 1) => "Д-р Джайлс удалится в комнату отдыха персонала на один час.",
        ("Уставший", 2) => "Д-р Джайлс заснёт на месте.",
        ("Уставший", 3) => "Д-ра Джайлса немедленно стошнит.",
        ("Довольный", 1) => "Д-р Джайлс начнёт напевать одну из своих любимых песен.",
        ("Довольный", 2) => "Д-р Джайлс займётся организацией пикника для сотрудников.",
        ("Довольный", 3) => "Д-р Джайлс впадёт в состояние сильной эйфории на 10-20 минут.",
        (_, 1) => $"Д-р Джайлс слегка оживится ({mood.ToLowerInvariant()}).",
        (_, 2) => $"Состояние д-ра Джайлса выравнивается ({mood.ToLowerInvariant()}).",
        _ => "Реакции не зафиксировано.",
    };

    public override void OnInput(in SceneInput input)
    {
        if (input.Kind != SceneInputKind.MouseClick)
        {
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            (int bx, int by, int bw, int bh) = ButtonRect(i);
            if (input.X >= bx && input.X < bx + bw && input.Y >= by && input.Y < by + bh)
            {
                PressButton(i + 1);
                return;
            }
        }
    }

    private int _height = 400;

    private (int X, int Y, int W, int H) ButtonRect(int index) => (10 + (index * 70), _height - 40, 60, 28);

    public override void Draw(ICanvas canvas)
    {
        _height = canvas.Height;
        canvas.Clear(Rgba.FromArgb(255, 12, 18, 12));

        int cx = canvas.Width / 2;
        int cy = canvas.Height / 2;

        // Face: two eyes (circles) and a mouth (line).
        int eyeR = 22;
        canvas.FillCircle(cx - 50, cy - 20, eyeR, Rgba.FromArgb(255, 230, 230, 180));
        canvas.FillCircle(cx + 50, cy - 20, eyeR, Rgba.FromArgb(255, 230, 230, 180));
        canvas.FillCircle(cx - 50, cy - 20, 10, Rgba.Black);
        canvas.FillCircle(cx + 50, cy - 20, 10, Rgba.Black);
        canvas.DrawLine(cx - 55, cy + 45, cx + 55, cy + 45, Rgba.FromArgb(255, 200, 180, 140));

        canvas.MeasureText(CurrentMood, 16, true, out int mw, out _);
        canvas.DrawText((canvas.Width - mw) / 2, cy - 110, CurrentMood, Rgba.White, 16, true);

        // Three buttons bottom-right.
        for (int i = 0; i < 3; i++)
        {
            (int x, int yy, int w, int h) = ButtonRect(i);
            canvas.FillRect(x, yy, w, h, Rgba.FromArgb(255, 40, 60, 40));
            canvas.DrawRect(x, yy, w, h, Rgba.Gray(160));
            canvas.DrawText(x + (w / 2) - 3, yy + 7, (i + 1).ToString(), Rgba.White, 13, true);
        }

        int y = 24;
        canvas.MeasureText("Hg", 11, false, out _, out int lh);
        int start = Math.Max(0, _log.Count - ((canvas.Height - 80) / lh));
        for (int k = start; k < _log.Count; k++)
        {
            canvas.DrawText(10, y, _log[k], Rgba.Gray(190), 11);
            y += lh;
        }

        canvas.DrawText(10, 6, "SCP-603-70 «Тамагочи» · СИМУЛЯЦИЯ (эффекты только описаны)", Rgba.FromArgb(255, 180, 220, 180), 10);
    }

    public IReadOnlyList<SceneEffect> DrainEffects()
    {
        var copy = _effects.ToArray();
        _effects.Clear();
        return copy;
    }
}
