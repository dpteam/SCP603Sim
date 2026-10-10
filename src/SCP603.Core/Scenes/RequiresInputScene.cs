using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-101 "Требуется Ввод" — a 13 character input box. In the record the entered concept
/// is erased from the operator's memory. Here the word is merely cleared and a disclaimer is
/// shown; no memory of the operator is touched in any way.
/// </summary>
public sealed class RequiresInputScene : SceneBase
{
    private readonly LineInput _input = new(13);
    private bool _received;
    private string _submitted = string.Empty;

    public override string Id => "SCP-603-101";

    public override string Name => "Требуется Ввод";

    public override string Title => "Требуется ввод";

    public override SceneKind Kind => SceneKind.Interactive;

    public bool Received => _received;

    public string Submitted => _submitted;

    public override void OnInput(in SceneInput input) => _input.Feed(input, Submit);

    public void Submit(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _submitted = text;
        _received = true; // the process ends after the input has been "received"
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);

        if (!_received)
        {
            canvas.MeasureText("ТРЕБУЕТСЯ ВВОД", 20, true, out int w, out _);
            canvas.DrawText((canvas.Width - w) / 2, canvas.Height / 2 - 40, "ТРЕБУЕТСЯ ВВОД", Rgba.White, 20, true);
            canvas.FillRect(canvas.Width / 2 - 90, canvas.Height / 2, 180, 30, Rgba.FromArgb(255, 18, 18, 18));
            canvas.DrawRect(canvas.Width / 2 - 90, canvas.Height / 2, 180, 30, Rgba.Gray(150));
            canvas.DrawText(canvas.Width / 2 - 82, canvas.Height / 2 + 7, _input.Text + "_", Rgba.Green, 14);
            canvas.DrawText(canvas.Width / 2 - 90, canvas.Height / 2 + 40, $"({_input.Text.Length}/13)", Rgba.Gray(120), 11);
        }
        else
        {
            canvas.MeasureText("ВВОД ПОЛУЧЕН", 20, true, out int w, out _);
            canvas.DrawText((canvas.Width - w) / 2, canvas.Height / 2 - 30, "ВВОД ПОЛУЧЕН", Rgba.FromArgb(255, 120, 255, 120), 20, true);
            canvas.DrawText((canvas.Width - 380) / 2, canvas.Height / 2 + 6,
                "СИМУЛЯЦИЯ: слово сброшено только на экране. Память оператора не затрагивается.",
                Rgba.Gray(200), 12);
        }
    }
}
