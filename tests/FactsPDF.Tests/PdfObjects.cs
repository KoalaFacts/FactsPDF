using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FactsPDF.Tests;

// A small reader of the PDFs FactsPDF writes (classic cross-reference table, ASCII object syntax), enough to
// read the structure tree, the page content streams and the catalog back without the engine's own types.
internal sealed partial class PdfObjects
{
    private readonly Dictionary<int, string> objects = [];

    internal PdfObjects(byte[] pdf)
    {
        Text = Encoding.Latin1.GetString(pdf);
        foreach (Match match in ObjectPattern().Matches(Text))
            objects[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)] = match.Groups[2].Value;
    }

    internal string Text { get; }

    [GeneratedRegex(@"(?<![^\n])(\d+) 0 obj\n(.*?)\nendobj\n", RegexOptions.Singleline)]
    private static partial Regex ObjectPattern();

    internal string Catalog => objects.Values.Single(o => o.Contains("/Type /Catalog"));

    private static int Ref(string body, string key)
    {
        var match = Regex.Match(body, key + @" (\d+) 0 R");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
    }

    private static string Stream(string body)
    {
        var start = body.IndexOf("stream\n", StringComparison.Ordinal) + "stream\n".Length;
        var end = body.LastIndexOf("\nendstream", StringComparison.Ordinal);
        return body[start..end];
    }

    internal string Xmp => Stream(objects[Ref(Catalog, "/Metadata")]);

    private List<int> PageIds()
    {
        var pages = objects.Values.Single(o => o.Contains("/Type /Pages"));
        return Regex.Matches(Regex.Match(pages, @"/Kids \[(.*?)\]").Groups[1].Value, @"(\d+) 0 R")
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
    }

    internal int PageCount => PageIds().Count;

    internal string PageContent(int page) => Stream(objects[Ref(objects[PageIds()[page]], "/Contents")]);

    internal List<string> Annotations() => objects.Values.Where(o => o.Contains("/Type /Annot")).ToList();

    private string StructRoot => objects.Values.Single(o => o.Contains("/Type /StructTreeRoot"));

    internal int ParentTreeLength(int page)
    {
        var nums = Regex.Match(objects[Ref(StructRoot, "/ParentTree")], @"/Nums \[(.*)\] >>", RegexOptions.Singleline).Groups[1].Value;
        var match = Regex.Match(nums, @"(?:^| )" + page + @" \[(.*?)\]");
        return match.Success ? Regex.Count(match.Groups[1].Value, @"\d+ 0 R") : -1;
    }

    // The structure tree as Name(kid,kid): # a marked-content id, link an object reference to an annotation.
    internal string Tree() => Describe(Ref(StructRoot, "/K"), text: false);

    // The same with the text each element shows instead of #.
    internal string TreeText() => Describe(Ref(StructRoot, "/K"), text: true);

    private string Describe(int id, bool text)
    {
        var body = objects[id];
        var name = Regex.Match(body, @"/S /(\w+)").Groups[1].Value;
        var page = PageIds().IndexOf(Ref(body, "/Pg"));
        var kids = new List<string>();
        var k = Regex.Match(body, @"/K \[(.*?)\] >>", RegexOptions.Singleline).Groups[1].Value;
        foreach (Match kid in Regex.Matches(k, @"<<.*?>>|\d+ 0 R|\d+"))
        {
            if (kid.Value.StartsWith("<<", StringComparison.Ordinal))
            {
                if (kid.Value.Contains("OBJR")) kids.Add("link");
                else
                {
                    var mcid = int.Parse(Regex.Match(kid.Value, @"/MCID (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
                    kids.Add(text ? Shown(PageIds().IndexOf(Ref(kid.Value, "/Pg")), mcid) : "#");
                }
            }
            else if (kid.Value.EndsWith(" R", StringComparison.Ordinal))
                kids.Add(Describe(int.Parse(kid.Value[..^2].Trim().Split(' ')[0], CultureInfo.InvariantCulture), text));
            else
                kids.Add(text ? Shown(page, int.Parse(kid.Value, CultureInfo.InvariantCulture)) : "#");
        }
        return name + "(" + string.Join(",", kids) + ")";
    }

    // The text a marked-content sequence shows, read through the font's ToUnicode map.
    private string Shown(int page, int mcid)
    {
        var content = PageContent(page);
        var start = content.IndexOf("<</MCID " + mcid + ">> BDC", StringComparison.Ordinal);
        var end = content.IndexOf("\nEMC", start, StringComparison.Ordinal);
        var map = new Dictionary<string, string>();
        foreach (var body in objects.Values.Where(o => o.Contains("beginbfchar")))
            foreach (Match entry in Regex.Matches(body, "<([0-9A-F]{4})> <([0-9A-F]{4})>"))
                map[entry.Groups[1].Value] = ((char)Convert.ToInt32(entry.Groups[2].Value, 16)).ToString();
        var builder = new StringBuilder();
        foreach (Match run in Regex.Matches(content[start..end], "<([0-9A-F]+)>"))
        {
            var hex = run.Groups[1].Value;
            for (var i = 0; i + 4 <= hex.Length; i += 4) builder.Append(map.GetValueOrDefault(hex.Substring(i, 4), "?"));
        }
        return builder.ToString();
    }
}
