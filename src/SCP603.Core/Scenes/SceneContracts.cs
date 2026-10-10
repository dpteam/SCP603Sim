using Scp603.Core.Morphik;
using Scp603.Core.Rendering;

namespace Scp603.Core.Scenes;

public enum SceneKind
{
    TextAdventure,
    Conversation,
    Visual,
    Interactive,
    Notification,
}

public enum SceneStatus
{
    NotStarted,
    Running,
    Finished,
    Crashed,
}

public enum SceneInputKind
{
    Text,
    Key,
    MouseClick,
    MouseMove,
}

public readonly record struct SceneInput(SceneInputKind Kind, string Text = "", int X = 0, int Y = 0, string Key = "")
{
    public static SceneInput Text_(string text) => new(SceneInputKind.Text, text);

    public static SceneInput Key_(string key) => new(SceneInputKind.Key, Key: key);

    public static SceneInput Click(int x, int y) => new(SceneInputKind.MouseClick, X: x, Y: y);
}

/// <summary>
/// A "scene" produced by SCP-603 when a qualifying prime is reached. Every scene is a
/// completely harmless simulation: the folklore effects are described, never performed.
/// </summary>
public interface IScene
{
    string Id { get; }

    string Name { get; }

    string Title { get; }

    SceneKind Kind { get; }

    SceneStatus Status { get; }

    bool Uses3D { get; }

    void Start(MorphikProfile profile, ulong sceneSeed);

    void Update(double deltaSeconds);

    void OnInput(in SceneInput input);

    void Draw(ICanvas canvas);

    void Stop();
}

/// <summary>Convenience base with safe defaults.</summary>
public abstract class SceneBase : IScene
{
    protected MorphikProfile Profile { get; private set; } = new();

    protected ulong Seed { get; private set; }

    public abstract string Id { get; }

    public abstract string Name { get; }

    public virtual string Title => Name;

    public abstract SceneKind Kind { get; }

    public SceneStatus Status { get; protected set; } = SceneStatus.NotStarted;

    public virtual bool Uses3D => false;

    public virtual void Start(MorphikProfile profile, ulong sceneSeed)
    {
        Profile = profile;
        Seed = sceneSeed;
        Status = SceneStatus.Running;
        OnStart();
    }

    protected virtual void OnStart()
    {
    }

    public virtual void Update(double deltaSeconds)
    {
    }

    public virtual void OnInput(in SceneInput input)
    {
    }

    public abstract void Draw(ICanvas canvas);

    public virtual void Stop()
    {
        if (Status == SceneStatus.Running)
        {
            Status = SceneStatus.Finished;
        }
    }

    /// <summary>Greedy word wrap using the canvas' own metrics.</summary>
    protected static List<string> Wrap(ICanvas canvas, string text, int maxWidth, int size, bool bold = false)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        foreach (string paragraph in text.Split('\n'))
        {
            string[] words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = new System.Text.StringBuilder();
            foreach (string word in words)
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                canvas.MeasureText(candidate, size, bold, out int w, out _);
                if (w > maxWidth && current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(word);
            }

            lines.Add(current.ToString());
        }

        return lines;
    }

    /// <summary>Draws wrapped text and returns the total height consumed.</summary>
    protected static int DrawWrapped(ICanvas canvas, int x, int y, int maxWidth, string text, uint color, int size, bool bold = false)
    {
        List<string> lines = Wrap(canvas, text, maxWidth, size, bold);
        canvas.MeasureText("Hg", size, bold, out _, out int lineHeight);
        int cy = y;
        foreach (string line in lines)
        {
            canvas.DrawText(x, cy, line, color, size, bold);
            cy += lineHeight;
        }

        return cy - y;
    }
}

public static class SceneExtensions
{
    /// <summary>Renders any scene to a headless text canvas (used for logs and tests).</summary>
    public static string ToText(this IScene scene, int width = 640, int height = 400)
    {
        var canvas = new TextCanvas(width, height);
        canvas.Clear(Rgba.Black);
        scene.Draw(canvas);
        return canvas.Render();
    }
}
