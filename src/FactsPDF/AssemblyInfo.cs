using System.Runtime.CompilerServices;

// Internals are visible only to tests and to in-repo verification tools. Public consumers,
// including the first-use example and the benchmarks, use the Pdf.Write() SDK.
[assembly: InternalsVisibleTo("FactsPDF.Tests")]
[assembly: InternalsVisibleTo("FactsPDF.CssSyntax.Benchmarks")]
[assembly: InternalsVisibleTo("FactsPDF.SyntaxProbe")]
// The CLI converts through the public SDK and reads only engine default limits internally.
[assembly: InternalsVisibleTo("FactsPDF.Cli")]
// Verification probes and the acceptance host drive exact engine options, including invalid
// edge values, to test engine-level limits and error codes.
[assembly: InternalsVisibleTo("FactsPDF.Acceptance")]
[assembly: InternalsVisibleTo("FactsPDF.WasmProbe")]
[assembly: InternalsVisibleTo("FactsPDF.CombinedReport")]
[assembly: InternalsVisibleTo("FactsPDF.FontStyles")]
[assembly: InternalsVisibleTo("FactsPDF.Images")]
[assembly: InternalsVisibleTo("FactsPDF.Lists")]
[assembly: InternalsVisibleTo("FactsPDF.PageMargins")]
[assembly: InternalsVisibleTo("FactsPDF.PageNumbers")]
[assembly: InternalsVisibleTo("FactsPDF.TableRowFragments")]
[assembly: InternalsVisibleTo("FactsPDF.Tables")]
