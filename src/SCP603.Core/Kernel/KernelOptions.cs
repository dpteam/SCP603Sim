using Scp603.Core.Scenes;

namespace Scp603.Core.Kernel;

public sealed class KernelOptions
{
    /// <summary>Version the running instance starts as.</summary>
    public int StartVersion { get; set; } = 1;

    public string BaseDirectory { get; set; } = Directory.GetCurrentDirectory();

    public string SourceDirectoryName { get; set; } = "source";

    public string OutputDirectoryName { get; set; } = "output";

    public bool EnableSourceForge { get; set; } = true;

    /// <summary>How many new versions the background forge tries to produce.</summary>
    public int ForgeTargetVersions { get; set; } = 6;

    public int ForgeStepDelayMs { get; set; } = 40;

    /// <summary>Artificial delay between emitted primes (0 = as fast as possible).</summary>
    public int PrimeDelayMs { get; set; }

    /// <summary>Only regenerate source code, do not run the prime thread.</summary>
    public bool SourceOnly { get; set; }

    public bool JournalToFile { get; set; } = true;

    public SceneHostOptions Scenes { get; set; } = new();

    public string SourceDirectory => Path.Combine(BaseDirectory, SourceDirectoryName);

    public string OutputDirectory => Path.Combine(BaseDirectory, OutputDirectoryName);

    public string JournalPath => Path.Combine(BaseDirectory, "runtime", "journal.log");
}
