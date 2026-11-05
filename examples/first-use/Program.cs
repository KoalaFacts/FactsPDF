using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using FactsPDF;

// Standalone PackageReference consumer of the public SDK. ToFile renders completely before
// replacing the destination atomically, so a rendering error preserves any existing file.
if (args.Length != 2) { Console.Error.WriteLine("Usage: FirstUse input.html output.pdf"); return 2; }
try
{
    // Private acceptance mode: inspect the staged CLI without invoking its entry point.
    if (args[0] == "--inspect-cli")
    {
        var cli = Assembly.LoadFile(Path.GetFullPath(args[1]));
        Console.WriteLine(JsonSerializer.Serialize(new { assembly = cli.Location,
            assemblyInformationalVersion = cli.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion }));
        return 0;
    }
    var result = Pdf.Write().ToFile(File.ReadAllText(args[0]), args[1]);
    Console.WriteLine(JsonSerializer.Serialize(new { pages = result.PageCount, bytes = result.BytesWritten,
        assembly = typeof(Pdf).Assembly.Location,
        assemblyIdentity = typeof(Pdf).Assembly.FullName,
        assemblyInformationalVersion = typeof(Pdf).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        runtime = new {
            version = Environment.Version.ToString(),
            framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            rid = RuntimeInformation.RuntimeIdentifier,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            targetFramework = AppContext.TargetFrameworkName,
            dynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported,
            dynamicCodeCompiled = RuntimeFeature.IsDynamicCodeCompiled
        } }));
    return 0;
}
catch (FactsPdfException error) { Console.Error.WriteLine(error.Message); return 3; }
catch (IOException error) { Console.Error.WriteLine(error.Message); return 4; }
