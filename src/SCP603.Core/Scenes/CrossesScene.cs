using Scp603.Core.Numerics;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-115 — three crosses in a row and a rapidly changing sequence of tones.
/// The tones are drawn as an animated spectrum; no audio is emitted and no effect is documented.
/// </summary>
public sealed class CrossesScene : SceneBase
{
    private static readonly int[] Scale = { 262, 294, 330, 349, 392, 440, 494, 523, 587, 659, 698, 784 };

    private XorShift64 _rng = new(1);
    private double _t;
    private int _toneIndex;
    private double _toneTimer;

    public int CurrentFrequency => Scale[_toneIndex];

    public override string Id => "SCP-603-115";

    public override string Name => "SCP-603-115";

    public override string Title => "Три креста";

    public override SceneKind Kind => SceneKind.Visual;

    protected override void OnStart()
    {
        _rng = new XorShift64(Seed);
        _t = 0;
        _toneIndex = 0;
    }

    public override void Update(double deltaSeconds)
    {
        _t += deltaSeconds;
        _toneTimer += deltaSeconds;
        if (_toneTimer >= 0.08)
        {
            _toneTimer = 0;
            _toneIndex = _rng.NextInt(0, Scale.Length);
        }
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);

        int crossW = 26;
        int crossH = 70;
        int gap = 90;
        int totalW = (crossW * 2) + (gap * 2) * 0 + (3 * (crossW * 2)) + (2 * gap);
        int startX = (canvas.Width - totalW) / 2 + crossW / 2;
        int cy = canvas.Height / 2 - 20;

        for (int i = 0; i < 3; i++)
        {
            int x = startX + (i * (crossW * 2 + gap));
            canvas.FillRect(x - crossW / 2, cy - crossH / 2, crossW, crossH, Rgba.Gray(230));
            canvas.FillRect(x - crossH / 2, cy - crossW / 2, crossH, crossW, Rgba.Gray(230));
        }

        // Tone bar.
        int barY = canvas.Height - 70;
        int barW = canvas.Width - 40;
        double norm = (_toneIndex + 1) / (double)Scale.Length;
        canvas.FillRect(20, barY, (int)(barW * norm), 14, Rgba.FromArgb(255, 90, 160, 255));
        canvas.DrawText(20, barY - 16, $"Тональность: {CurrentFrequency} Гц (только отображение)", Rgba.Gray(180), 11);
        canvas.DrawText(10, 6, "SCP-603-115 «Три креста» · отрицательных последствий не отмечено", Rgba.Gray(150), 11);
    }
}
