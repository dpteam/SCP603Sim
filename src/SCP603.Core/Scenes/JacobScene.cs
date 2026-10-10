using Scp603.Core.Numerics;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-20 "Яков" — a coloured equilateral triangle on black with a chat box underneath.
/// The entity "answers" deterministically and its memory never survives the process,
/// exactly as recorded. No claims of sentience are made by this simulation.
/// </summary>
public sealed class JacobScene : SceneBase
{
    private static readonly string[] FallbackPool =
    {
        "Не нравится - не ешь.",
        "Если подумаю хорошенько.",
        "Здесь темно.",
        "Я чего только не говорю.",
        "Вам нравится мозаика?",
        "Я родился вновь.",
        "Предложение не справляется с растущим спросом.",
        "Рад видеть уважаемого доктора брауна. А доктор браун - настоящий доктор, или он притворяется?",
    };

    private readonly LineInput _input = new(120);
    private readonly List<(string Who, string Text)> _transcript = new();
    private string _response = "Пожалуйста, введите запрос";

    public override string Id => "SCP-603-20";

    public override string Name => "Яков";

    public override string Title => "Hello, world!";

    public override SceneKind Kind => SceneKind.Conversation;

    public IReadOnlyList<(string Who, string Text)> Transcript => _transcript;

    public override void OnInput(in SceneInput input) => _input.Feed(input, Submit);

    public void Submit(string text)
    {
        _response = Respond(text);
        _transcript.Add(("Д-р", text));
        _transcript.Add(("Яков", _response));
        if (_transcript.Count > 200)
        {
            _transcript.RemoveRange(0, _transcript.Count - 200);
        }
    }

    /// <summary>Deterministic reply function: same input, same seed =&gt; same answer, always.</summary>
    public string Respond(string question)
    {
        string q = (question ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            return "Пожалуйста, введите запрос";
        }

        if (Contains(q, "здравствуй", "привет", "хай", "здоров")) return "Привет.";
        if (Contains(q, "как тебя", "твоё имя", "твое имя", "кто ты")) return "Яков.";
        if (Contains(q, "что ты такое", "что такое ты")) return "Всегда.";
        if (Contains(q, "понимаешь", "понимаеш", "понял")) return "Я понимаю тебя.";
        if (Contains(q, "живой", "живёшь", "живешь", "жив")) return "Да.";
        if (Contains(q, "как тебя зовут")) return "Яков.";
        if (Contains(q, "моё имя", "мое имя", "меня зовут", "знаешь моё")) return "Вас зовут Джозеф Браун.";
        if (Contains(q, "megaprime", "мегапрайм")) return "Я - порождение megaprime. С megaprime возможно всё.";
        if (Contains(q, "опасен", "опасн", "угроза")) return "Нет.";
        if (Contains(q, "больно", "боль")) return "Я испытываю невероятную боль.";
        if (Contains(q, "доверять", "доверя")) return "Не нравится - не ешь.";
        if (Contains(q, "компьютер", "программа")) return "Я - компьютерная программа. Вам нравится мозаика?";
        if (Contains(q, "создал", "создатель", "родил")) return "Я родился вновь.";
        if (Contains(q, "темно", "где ты")) return "Здесь темно.";
        if (Contains(q, "мозаик") || Contains(q, "устриц")) return "Рад, что мы достигли согласия.";
        if (Contains(q, "принцип", "как ты работаешь", "как работает")) return "Если подумаю хорошенько.";
        if (Contains(q, "внешн", "мир")) return "Предложение не справляется с растущим спросом.";

        ulong h = Fnv(q, Seed);
        return FallbackPool[(int)(h % (ulong)FallbackPool.Length)];
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (string needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ulong Fnv(string s, ulong seed)
    {
        ulong h = 1469598103934665603UL ^ seed;
        foreach (char c in s)
        {
            h ^= c;
            h *= 1099511628211UL;
        }

        return h;
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);

        int cx = canvas.Width / 2;
        int cy = canvas.Height / 2 - 10;
        int r = Math.Min(canvas.Width, canvas.Height) / 4;

        // Equilateral triangle vertices (0 top, then clockwise).
        int x0 = cx, y0 = cy - r;
        int x1 = cx + (int)(r * 0.866), y1 = cy + r / 2;
        int x2 = cx - (int)(r * 0.866), y2 = cy + r / 2;

        // Approximate the gradient fill by shading three sub-triangles about the centroid.
        int gx = (x0 + x1 + x2) / 3, gy = (y0 + y1 + y2) / 3;
        canvas.DrawTriangle(x0, y0, x1, y1, gx, gy, Rgba.Lerp(Rgba.Red, Rgba.Blue, 0.5), true);
        canvas.DrawTriangle(x1, y1, x2, y2, gx, gy, Rgba.Lerp(Rgba.Blue, Rgba.Green, 0.5), true);
        canvas.DrawTriangle(x2, y2, x0, y0, gx, gy, Rgba.Lerp(Rgba.Green, Rgba.Red, 0.5), true);

        // Vertex nodes in pure R/G/B.
        int nodeR = Math.Max(3, r / 14);
        canvas.FillCircle(x0, y0, nodeR, Rgba.Red);
        canvas.FillCircle(x1, y1, nodeR, Rgba.Blue);
        canvas.FillCircle(x2, y2, nodeR, Rgba.Green);

        // Answer above the triangle, in bold white.
        canvas.MeasureText(_response, 14, true, out int tw, out int th);
        canvas.DrawText((canvas.Width - tw) / 2, Math.Max(6, y0 - th - 14), _response, Rgba.White, 14, true);

        // Input box.
        int boxH = 26;
        int boxY = canvas.Height - boxH - 8;
        canvas.FillRect(8, boxY, canvas.Width - 16, boxH, Rgba.FromArgb(255, 16, 16, 16));
        canvas.DrawRect(8, boxY, canvas.Width - 16, boxH, Rgba.Gray(90));
        canvas.DrawText(14, boxY + 6, _input.Text + "_", Rgba.Gray(220), 13);

        canvas.DrawText(8, 6, "Яков  ·  SCP-603-20  ·  память стирается при завершении процесса", Rgba.Gray(110), 10);
    }
}
