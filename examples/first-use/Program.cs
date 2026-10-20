using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FactsPDF;

// Standalone PackageReference consumer. Convert before opening the destination
// so rendering errors preserve it. Final file I/O is not an atomic-write policy.
if (args.Length != 2) { Console.Error.WriteLine("Usage: FirstUse input.html output.pdf"); return 2; }
try
{
    using var buffer = new MemoryStream();
    var result = PdfConverter.Convert(File.ReadAllText(args[0]), buffer, new PdfOptions());
    File.WriteAllBytes(args[1], buffer.ToArray());
    Console.WriteLine(JsonSerializer.Serialize(new { pages = result.PageCount, bytes = result.BytesWritten,
        assembly = typeof(PdfConverter).Assembly.Location,
        assemblyIdentity = typeof(PdfConverter).Assembly.FullName,
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
