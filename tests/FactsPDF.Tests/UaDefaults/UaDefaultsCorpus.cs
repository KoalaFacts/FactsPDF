using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FactsPDF.Tests.UaDefaults;

/// <summary>
/// The committed UA-default probe corpus: one tiny synthetic document per element or context.
/// Only font-family is authored, so every computed value is a user-agent default.
/// </summary>
internal sealed record UaProbe(string Id, string Body, IReadOnlyList<string> Targets)
{
    /// <summary>The element a target key names: <c>tag#n</c> is the n-th element with that tag in document order.</summary>
    internal static (string Tag, int Index) ParseTarget(string key)
    {
        var hash = key.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? (key, 0) : (key[..hash], int.Parse(key[(hash + 1)..], CultureInfo.InvariantCulture));
    }
}

internal sealed class UaCorpus
{
    internal required string DocumentTemplate { get; init; }
    internal required string AuthoredStyle { get; init; }
    internal required string ImageName { get; init; }
    internal required IReadOnlyList<UaProbe> Probes { get; init; }

    /// <summary>The directory holding the committed fixtures: the copy next to the test assembly, or the source copy when updating.</summary>
    internal static string FixtureDirectory(bool source)
    {
        if (!source) return Path.Combine(AppContext.BaseDirectory, "ua-defaults");
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "ua-defaults");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("tests/fixtures/ua-defaults was not found above the test assembly.");
    }

    internal static UaCorpus Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDirectory(false), "corpus.json")));
        var root = document.RootElement;
        return new UaCorpus
        {
            DocumentTemplate = root.GetProperty("documentTemplate").GetString()!,
            AuthoredStyle = root.GetProperty("authoredStyle").GetString()!,
            ImageName = root.GetProperty("imageName").GetString()!,
            Probes = [.. root.GetProperty("probes").EnumerateArray().Select(probe => new UaProbe(
                probe.GetProperty("id").GetString()!,
                probe.GetProperty("body").GetString()!,
                [.. probe.GetProperty("targets").EnumerateArray().Select(target => target.GetProperty("key").GetString()!)]))],
        };
    }

    internal string Html(UaProbe probe)
        => DocumentTemplate.Replace("{{style}}", AuthoredStyle, StringComparison.Ordinal)
            .Replace("{{body}}", probe.Body, StringComparison.Ordinal);

    /// <summary>The source offset of a target's start tag in the probe's document.</summary>
    internal static int Offset(string html, string key)
    {
        var (tag, index) = UaProbe.ParseTarget(key);
        var matches = Regex.Matches(html, "<" + Regex.Escape(tag) + @"(?=[\s/>])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return matches[index].Index;
    }
}
