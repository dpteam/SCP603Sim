using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

/// <summary>
/// SCP-603-508 "Джин, детям грозит беда" — a variant of Яков with a 1px diagonal line and
/// a fixed, always identical dialogue that is advanced one line per Enter press.
/// </summary>
public sealed class Jacob508Scene : SceneBase
{
    private static readonly (string Doctor, string Jacob)[] Dialogue =
    {
        ("здравствуйте", "Привет."),
        ("кто ты?", "Яков."),
        ("Что ты такое?", "Всегда."),
        ("ты меня понимаешь?", "Я понимаю тебя."),
        ("ты живой?", "Не нравится - не ешь."),
        ("меня зовут д-р браун", "Рад видеть уважаемого доктора брауна. А доктор браун - настоящий доктор, или он притворяется?"),
        ("Я настоящий доктор", "Не нравится - не ешь."),
        ("ты можешь мне рассказать про megaprime?", "Да. Что такое megaprime?"),
        ("ты - порождение megaprime", "Я могу вам обо всём этом рассказать."),
        ("и что ты можешь сказать мне про megaprime?", "Здесь темно."),
        ("опасен ли megaprime?", "Нет."),
        ("а сам ты опасен?", "Нет."),
        ("могу я тебе доверять?", "Не нравится - не ешь."),
        ("почему ты так часто говоришь эту фразу?", "Я чего только не говорю."),
        ("ты знаешь что-нибудь о внешнем мире?", "Да."),
        ("и что ты знаешь?", "Предложение не справляется с растущим спросом."),
        ("ты помнишь моё имя?", "Вас зовут Джозеф Браун."),
        ("откуда ты знаешь моё имя?", "Откуда я его знаю?"),
        ("да", "Я знаю о том, что было."),
        ("а что ещё ты обо мне знаешь?", "Вас зовут Джозеф Браун. Вы - доктор и не притворяетесь."),
        ("каков принцип твоей работы?", "Если подумаю хорошенько."),
        ("как ты работаешь?", "Если подумаю хорошенько."),
        ("ты - компьютерная программа.", "Я - компьютерная программа. Вам нравится мозаика?"),
        ("да", "Рад, что мы достигли согласия."),
        ("кто тебя создал?", "Я родился вновь."),
        ("ты живой?", "Да."),
        ("Как это возможно?", "Не нравится - не ешь."),
        ("Как это возможно?", "С megaprime возможно всё."),
        ("что такое megaprime?", "Я - порождение megaprime. С megaprime возможно всё."),
        ("ты доволен?", "Нет."),
        ("почему нет?", "Я испытываю невероятную боль."),
        ("отчего тебе больно?", "[окно закрылось]"),
    };

    private readonly List<string> _lines = new();
    private int _index;

    public override string Id => "SCP-603-508";

    public override string Name => "Джин, детям грозит беда";

    public override string Title => "Hello, world!";

    public override SceneKind Kind => SceneKind.Conversation;

    public int Index => _index;

    protected override void OnStart()
    {
        _lines.Clear();
        _index = 0;
    }

    public override void OnInput(in SceneInput input)
    {
        bool enter = (input.Kind == SceneInputKind.Key && input.Key is "Enter" or "Return")
            || (input.Kind == SceneInputKind.Text && input.Text is "\n" or "\r" or "Enter");
        if (enter)
        {
            Advance();
        }
    }

    /// <summary>Simulates one press of the only working button.</summary>
    public void Advance()
    {
        if (_index < Dialogue.Length)
        {
            (string doctor, string jacob) = Dialogue[_index];
            _lines.Add("Д-р Браун: " + doctor);
            _lines.Add("Яков: " + jacob);
            _index++;
            if (_index == Dialogue.Length)
            {
                Status = SceneStatus.Finished;
            }

            return;
        }

        _lines.Add("null");
    }

    public override void Draw(ICanvas canvas)
    {
        canvas.Clear(Rgba.Black);

        // The signature 1px black diagonal (drawn last so it crosses everything).
        int y = 26;
        canvas.MeasureText("Hg", 12, false, out _, out int lineHeight);
        int available = Math.Max(1, (canvas.Height - 60) / lineHeight);
        int start = Math.Max(0, _lines.Count - available);
        for (int k = start; k < _lines.Count; k++)
        {
            uint color = _lines[k].StartsWith("Яков:", StringComparison.Ordinal) ? Rgba.White : Rgba.Gray(170);
            canvas.DrawText(10, y, _lines[k], color, 12, _lines[k].StartsWith("Яков:", StringComparison.Ordinal));
            y += lineHeight;
        }

        canvas.DrawText(10, 6, "SCP-603-508 · «Джин, детям грозит беда» · нажмите Enter", Rgba.Gray(150), 11, true);
        canvas.DrawText(10, canvas.Height - 20, "[Enter] — единственная работающая кнопка", Rgba.Gray(120), 11);

        for (int x = 0; x < canvas.Width; x++)
        {
            int dy = (int)((long)x * canvas.Height / Math.Max(1, canvas.Width));
            canvas.DrawLine(x, dy, x, dy, Rgba.Black);
        }
    }
}
