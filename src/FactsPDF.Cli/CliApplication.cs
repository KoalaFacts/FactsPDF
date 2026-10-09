namespace FactsPDF.CommandLine;

public static class CliApplication
{
    // Intentional red baseline for CLI behavior; replaced in the following implementation.
    public static int Run(string[] args, Stream input, Stream output, TextWriter error,
        CancellationToken cancellationToken = default) => 0;
}
