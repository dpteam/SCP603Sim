using Scp603.Core.Numerics;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-95 — the rapidly changing random imagery. The memorised effects (trance state,
/// bleeding, memory consumption) are represented only as numbers on screen: nothing is done
/// to the observer, and the "memory" that grows is a counter inside the simulation.
/// </summary>
public sealed class VisualNoiseScene : SceneBase
{
    private XorShift64 _rng = new(1);
    private double _memoryMb = 64;
    private double _elapsed;
    private double _frameTimer;

    public double MemoryMb => _memoryMb;

    public double CapMb { get; set; } = 2048;

    public double ElapsedSeconds => _elapsed;

    public override string Id => "SCP-603-95";

    public override string Name => "SCP-603-95";

    public override string Title => "SCP-603-95";

    public override SceneKind Kind => SceneKind.Visual;

    public override bool Uses3D => Profile.Uses3D;

    protected override void OnStart()
    {
        _rng = new XorShift64(Seed);
        _memoryMb = 64;
        _elapsed = 0;
        _frameTimer = 0;
    }

    public override void Update(double deltaSeconds)
    {
        if (Status != SceneStatus.Running)
        {
            return;
        }

        _elapsed += deltaSeconds;
        _frameTimer += deltaSeconds;
        double rate = _memoryMb > 512 ? 0.31 : 0.12;
        _memoryMb *= 1 + (rate * deltaSeconds);

        if (_memoryMb >= CapMb)
        {
            _memoryMb = CapMb;
            Status = SceneStatus.Crashed;
        }
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);

        // Random imagery, seeded so the same version always shows the same thing.
        _rng.NextUInt64();
        var frame = new XorShift64(Seed ^ (ulong)(long)(_frameTimer * 4));
        int blocks = 30;
        for (int i = 0; i < blocks; i++)
        {
            int x = frame.NextInt(0, canvas.Width);
            int y = frame.NextInt(0, canvas.Height);
            int w = frame.NextInt(8, canvas.Width / 3 + 1);
            int h = frame.NextInt(8, canvas.Height / 3 + 1);
            uint color = Rgba.FromArgb(255, frame.NextInt(0, 256), frame.NextInt(0, 256), frame.NextInt(0, 256));
            canvas.FillRect(x, y, w, h, color);
        }

        canvas.FillRect(0, 0, canvas.Width, 22, Rgba.FromArgb(255, 30, 30, 0));
        canvas.DrawText(8, 5, "SCP-603-95  ·  СИМУЛЯЦИЯ: воздействие на зрителя не воспроизводится", Rgba.FromArgb(255, 255, 220, 120), 11, true);

        int barW = 240;
        int barY = canvas.Height - 40;
        canvas.FillRect(10, barY, barW, 16, Rgba.Gray(30));
        double ratio = Math.Clamp(_memoryMb / CapMb, 0, 1);
        canvas.FillRect(10, barY, (int)(barW * ratio), 16, ratio > 0.25 ? Rgba.FromArgb(255, 220, 60, 60) : Rgba.FromArgb(255, 60, 200, 60));
        canvas.DrawText(10, barY - 16, $"ОЗУ: {_memoryMb:F0} МБ  (рост {( _memoryMb > 512 ? "31" : "12")} %/с)", Rgba.Gray(220), 11);

        if (Status == SceneStatus.Crashed)
        {
            canvas.FillRect(0, canvas.Height / 2 - 14, canvas.Width, 28, Rgba.FromArgb(255, 60, 0, 0));
            canvas.DrawText(12, canvas.Height / 2 - 8, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ (память исчерпана, симуляция)", Rgba.White, 12, true);
        }
    }
}
