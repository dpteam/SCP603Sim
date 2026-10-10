using System.Text;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-44 "Умереть" — the interactive text adventure. In the Foundation's record the
/// command "умереть" is lethal. Here it is strictly fictional: the scene prints the same
/// words and stops, and a permanent banner states that nothing real is happening.
/// </summary>
public sealed class DieScene : SceneBase
{
    public const string IntroText =
        "Ты продираешься сквозь колючие заросли, а сердце колотится всё сильнее. " +
        "Оглянувшись, ты видишь сквозь пелену дыма старый маяк на севере, слабейший проблеск надежды, " +
        "безжалостно сведённый на нет твоей же собственной глупостью. К этой далёкой, недостижимой мечте " +
        "тебе уже никогда не вернуться, и ты это знаешь.";

    public const string DeathText =
        "Мир вокруг тебя проваливается в черноту, и ты знаешь, что получил по заслугам.";

    public const string SimulationNote =
        "[СИМУЛЯЦИЯ] Никакого воздействия на зрителя не производится. Персонаж игры «умер»; процесс остановлен.";

    private readonly List<string> _history = new();
    private readonly StringBuilder _input = new();
    private bool _died;

    public override string Id => "SCP-603-44";

    public override string Name => "Умереть";

    public override string Title => "Умереть";

    public override SceneKind Kind => SceneKind.TextAdventure;

    protected override void OnStart()
    {
        _history.Clear();
        _history.Add(IntroText);
    }

    public void TypeChar(char c)
    {
        if (_died || char.IsControl(c))
        {
            return;
        }

        _input.Append(c);
    }

    public void Backspace()
    {
        if (_input.Length > 0)
        {
            _input.Length--;
        }
    }

    public void Submit()
    {
        if (_died)
        {
            return;
        }

        Execute(_input.ToString());
        _input.Clear();
    }

    public void Execute(string command)
    {
        string cmd = command.Trim();
        _history.Add("> " + cmd);
        string norm = cmd.ToLowerInvariant();

        if (norm.Length == 0)
        {
            _history.Add("Ты стоишь на месте.");
            return;
        }

        if (norm is "оглядеться" or "осмотреться" or "смотреть")
        {
            _history.Add(IntroText);
            return;
        }

        if (norm is "умереть" or "die")
        {
            _history.Add(DeathText);
            _history.Add(SimulationNote);
            _died = true;
            Status = SceneStatus.Finished;
            return;
        }

        if (norm.StartsWith("идти ", StringComparison.Ordinal) || norm.StartsWith("пойти ", StringComparison.Ordinal)
            || norm.StartsWith("go ", StringComparison.Ordinal))
        {
            string dir = norm[(norm.IndexOf(' ') + 1)..];
            if (dir.StartsWith("на ", StringComparison.Ordinal) || dir.StartsWith("в ", StringComparison.Ordinal)
                || dir.StartsWith("к ", StringComparison.Ordinal))
            {
                _history.Add($"Ты не можешь пойти {dir}.");
            }
            else
            {
                _history.Add($"Ты не можешь пойти на {dir}.");
            }

            return;
        }

        if (norm.StartsWith("поднять ", StringComparison.Ordinal) || norm.StartsWith("взять ", StringComparison.Ordinal))
        {
            string obj = norm[(norm.IndexOf(' ') + 1)..];
            _history.Add($"Здесь нет ничего похожего на «{obj}».");
            return;
        }

        _history.Add($"Ты не можешь {norm}.");
    }

    public override void OnInput(in SceneInput input)
    {
        switch (input.Kind)
        {
            case SceneInputKind.Text:
                if (input.Text == "\u0008" || input.Text == "Backspace")
                {
                    Backspace();
                }
                else if (input.Text is "\n" or "\r" or "Enter")
                {
                    Submit();
                }
                else if (input.Text.Length == 1)
                {
                    TypeChar(input.Text[0]);
                }
                else
                {
                    Execute(input.Text);
                }

                break;

            case SceneInputKind.Key:
                if (input.Key == "Backspace")
                {
                    Backspace();
                }
                else if (input.Key is "Enter" or "Return")
                {
                    Submit();
                }

                break;
        }
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);
        canvas.FillRect(0, 0, canvas.Width, 24, Rgba.FromArgb(255, 40, 0, 0));
        canvas.DrawText(8, 6, "УМЕРЕТЬ  ·  SCP-603-44  ·  СИМУЛЯЦИЯ (воздействие не воспроизводится)", Rgba.FromArgb(255, 255, 180, 180), 11, true);

        int y = 34;
        canvas.MeasureText("Hg", 12, false, out _, out int lineHeight);
        int maxWidth = canvas.Width - 20;

        // Only the most recent lines fit; show from the tail.
        int availableLines = Math.Max(1, (canvas.Height - 70) / lineHeight);
        int start = Math.Max(0, _history.Count - availableLines);
        for (int k = start; k < _history.Count; k++)
        {
            foreach (string line in Wrap(canvas, _history[k], maxWidth, 12))
            {
                canvas.DrawText(10, y, line, Rgba.Gray(220), 12);
                y += lineHeight;
            }
        }

        canvas.FillRect(0, canvas.Height - 28, canvas.Width, 28, Rgba.FromArgb(255, 20, 20, 20));
        string prompt = _died ? "(процесс остановлен)" : "» " + _input;
        canvas.DrawText(10, canvas.Height - 21, prompt, _died ? Rgba.Gray(120) : Rgba.Green, 12, true);
    }
}
