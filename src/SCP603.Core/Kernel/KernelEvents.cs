namespace Scp603.Core.Kernel;

public readonly record struct PrimeEvent(long Value, int Index, string Formatted, long SequenceNumber);

public readonly record struct WindowEvent(long Prime, int WindowNumber, string SceneId, string SceneName);

public readonly record struct VersionEvent(
    int Version,
    string SourcePath,
    ulong Digest,
    ulong ParentDigest = 0,
    string MutationSummary = "",
    string GeneratedIconPath = "",
    string GeneratedRendererPath = "");
