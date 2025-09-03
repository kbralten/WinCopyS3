using System;
using System.IO;
using System.Linq;
using System.Reflection;

class Program
{
    static int Main(string[] args)
    {
        var repoRoot = Directory.GetCurrentDirectory();
        var relative = args.Length > 0 ? args[0] : Path.Combine("..", "src", "WinCopyS3", "bin", "Release", "net8.0-windows", "WinCopyS3.dll");
        var dll = Path.GetFullPath(Path.Combine(repoRoot, relative));
        if (!File.Exists(dll))
        {
            // try to find any WinCopyS3.dll under repo
            var candidates = Directory.GetFiles(repoRoot, "WinCopyS3.dll", SearchOption.AllDirectories);
            if (candidates.Length == 0)
            {
                Console.Error.WriteLine($"Could not find WinCopyS3.dll under {repoRoot}. Build the project first.");
                return 2;
            }
            dll = candidates[0];
        }

        Console.WriteLine($"Loading assembly: {dll}");

        // Load assembly into a new AssemblyLoadContext to avoid runtime mismatch
        try
        {
            var alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
            if (alcType != null)
            {
                // Use default context to allow proper binding
            }
        }
        catch { }

        Assembly asm;
        try
        {
            asm = Assembly.LoadFrom(dll);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load assembly: {ex.Message}");
            return 3;
        }

        var type = asm.GetType("WinCopyS3.ETWEvents");
        if (type == null)
        {
            Console.Error.WriteLine("Type WinCopyS3.ETWEvents not found in assembly. Available types (first 50):");
            foreach (var t in asm.GetTypes().Take(50)) Console.WriteLine(" - " + t.FullName);
            return 4;
        }

        // Try field
        var f = type.GetField("ProviderGuid", BindingFlags.Static | BindingFlags.Public);
        if (f != null)
        {
            var val = f.GetValue(null)?.ToString();
            Console.WriteLine($"Provider GUID (field): {val}");
            return 0;
        }

        var p = type.GetProperty("ProviderGuid", BindingFlags.Static | BindingFlags.Public);
        if (p != null)
        {
            var val = p.GetValue(null)?.ToString();
            Console.WriteLine($"Provider GUID (property): {val}");
            return 0;
        }

        Console.Error.WriteLine("No static ProviderGuid field or property found on WinCopyS3.ETWEvents.");
        return 5;
    }
}
