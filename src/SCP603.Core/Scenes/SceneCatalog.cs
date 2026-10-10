namespace Scp603.Core.Scenes;

/// <summary>Options passed to scenes that need to look outside themselves.</summary>
public sealed class SceneHostOptions
{
    public IReadOnlyList<string> SearchDirectories { get; set; } = Array.Empty<string>();

    public bool AllowBroadScan { get; set; }
}

/// <summary>Registry of every documented scene and how to build it.</summary>
public static class SceneCatalog
{
    public const string DieId = "SCP-603-44";
    public const string JacobId = "SCP-603-20";
    public const string VisualNoiseId = "SCP-603-95";
    public const string TamagotchiId = "SCP-603-70";
    public const string RequiresInputId = "SCP-603-101";
    public const string CrossesId = "SCP-603-115";
    public const string CountdownId = "SCP-603-116";
    public const string PrinterId = "SCP-603-118";
    public const string TellerId = "SCP-603-121";
    public const string Jacob508Id = "SCP-603-508";
    public const string IsThisId = "SCP-603-734";

    public static readonly string[] AllIds =
    {
        DieId, JacobId, VisualNoiseId, TamagotchiId, RequiresInputId, CrossesId,
        CountdownId, PrinterId, TellerId, Jacob508Id, IsThisId,
    };

    public static IScene Create(string id, SceneHostOptions? options = null)
    {
        options ??= new SceneHostOptions();
        return id switch
        {
            DieId => new DieScene(),
            JacobId => new JacobScene(),
            VisualNoiseId => new VisualNoiseScene(),
            TamagotchiId => new TamagotchiScene(),
            RequiresInputId => new RequiresInputScene(),
            CrossesId => new CrossesScene(),
            CountdownId => new CountdownScene(),
            PrinterId => new PrinterScene(),
            TellerId => new TellerScene(options.SearchDirectories, options.AllowBroadScan),
            Jacob508Id => new Jacob508Scene(),
            IsThisId => new IsThisScene(),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Неизвестная сцена"),
        };
    }

    public static string NameOf(string id) => Create(id).Name;

    public static string TitleOf(string id) => Create(id).Title;
}
