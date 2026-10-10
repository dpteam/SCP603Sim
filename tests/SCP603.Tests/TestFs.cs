namespace Scp603.Tests;

public static class TestFs
{
    public static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "scp603-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void Cleanup(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
