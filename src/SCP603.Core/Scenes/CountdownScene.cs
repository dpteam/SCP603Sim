using Scp603.Core.Numerics;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-116 — a countdown from 256 with uneven steps. At -1 the record mentions a zone wide
/// power failure. Here the "failure" is a five second black screen inside this window only.
/// The facility is never touched.
/// </summary>
public sealed class CountdownScene : SceneBase, ISceneEffects
{
    private readonly List<SceneEffect> _effects = new();
    private XorShift64 _rng = new(1);
    private double _timer;
    private double _nextDelay;
    private double _failureRemaining;

    public int Value { get; private set; } = 256;

    public bool PowerFailure { get; private set; }

    public double FailureSeconds { get; set; } = 5.0;

    public override string Id => "SCP-603-116";

    public override string Name => "SCP-603-116";

    public override string Title => "Обратный отсчёт";

    public override SceneKind Kind => SceneKind.Visual;

    protected override void OnStart()
    {
        _rng = new XorShift64(Seed);
        Value = 256;
        _timer = 0;
        _nextDelay = NextDelay();
        PowerFailure = false;
        _failureRemaining = 0;
    }

    private double NextDelay() => 0.5 + (_rng.NextDouble() * 1.5);

    public override void Update(double deltaSeconds)
    {
        if (Status != SceneStatus.Running)
        {
            return;
        }

        if (PowerFailure)
        {
            _failureRemaining -= deltaSeconds;
            if (_failureRemaining <= 0)
            {
                PowerFailure = false;
                Status = SceneStatus.Finished;
            }

            return;
        }

        _timer += deltaSeconds;
        if (_timer >= _nextDelay)
        {
            _timer = 0;
            _nextDelay = NextDelay();
            Value--;
            if (Value == -1)
            {
                PowerFailure = true;
                _failureRemaining = FailureSeconds;
                _effects.Add(new SceneEffect("power-flicker", "5 с (симуляция, только это окно)"));
            }
        }
    }

    public override void Draw(ICanvas canvas)
    {
        if (PowerFailure)
        {
            canvas.Clear(Rgba.Black);
            canvas.MeasureText("СБОЙ ПИТАНИЯ (симуляция)", 18, true, out int w, out _);
            canvas.DrawText((canvas.Width - w) / 2, canvas.Height / 2 - 10, "СБОЙ ПИТАНИЯ (симуляция)", Rgba.FromArgb(255, 120, 30, 30), 18, true);
            canvas.DrawText(10, canvas.Height - 20, "Реальное электропитание не затронуто.", Rgba.Gray(120), 11);
            return;
        }

        canvas.Clear(Rgba.Black);
        string text = Value.ToString();
        canvas.MeasureText(text, 48, true, out int tw, out _);
        uint color = Value <= 10 ? Rgba.FromArgb(255, 255, 80, 80) : Rgba.White;
        canvas.DrawText((canvas.Width - tw) / 2, canvas.Height / 2 - 40, text, color, 48, true);
        canvas.DrawText(10, 6, "SCP-603-116 · отсчёт 256 → -1 (только визуализация)", Rgba.Gray(150), 11);
    }

    public IReadOnlyList<SceneEffect> DrainEffects()
    {
        var copy = _effects.ToArray();
        _effects.Clear();
        return copy;
    }
}
