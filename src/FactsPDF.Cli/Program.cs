using FactsPDF.CommandLine;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
return CliApplication.Run(args, Console.OpenStandardInput(), Console.OpenStandardOutput(), Console.Error, cancellation.Token);
