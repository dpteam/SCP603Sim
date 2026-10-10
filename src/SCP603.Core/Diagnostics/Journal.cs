using System.Globalization;
using System.Text;

namespace Scp603.Core.Diagnostics;

/// <summary>
/// Append-only log of everything the simulation does. Optionally mirrored to a file so that
/// a run can be audited later (mirrors the Foundation's insistence on supervision).
/// </summary>
public sealed class Journal : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter? _writer;

    public Journal(string? path = null)
    {
        if (!string.IsNullOrEmpty(path))
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        }
    }

    public event Action<string>? Written;

    public void Write(string message)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message;
        lock (_gate)
        {
            _writer?.WriteLine(line);
        }

        Written?.Invoke(line);
    }

    public void Dispose() => _writer?.Dispose();
}
