namespace Scp603.Tests;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("SCP-603: набор самотестов (без сторонних библиотек)");
        Console.WriteLine();

        PrimesTests.RegisterAll();
        MorphikTests.RegisterAll();
        MegaprimeTests.RegisterAll();
        KernelTests.RegisterAll();
        SceneTests.RegisterAll();
        EndToEndTests.RegisterAll();

        string? filter = args.Length > 0 ? args[0] : null;
        return TestHarness.RunAll(filter);
    }
}
