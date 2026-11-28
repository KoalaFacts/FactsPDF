using System.Buffers;
using System.Text;

namespace FactsPDF;

/// <summary>
/// The element tree Tagged PDF output is built from: one entry per element start tag with a source
/// position, in document order, found by walking the token list the document reader walks, with the same
/// implied end tags (the paragraph and list item rules shared with <see cref="HtmlElementPositions"/>).
/// A text run is attributed to its element through the source offset of the element that holds it (a
/// glyph carries the offset of its innermost open element), so layout needs no structure bookkeeping.
/// </summary>
/// <remarks>
/// The arrays are rented from the shared pool and returned by <see cref="Release"/>; a structure that is
/// never released only costs the pool a new array. Every array is indexed by element.
/// </remarks>
internal sealed class DocumentStructure
{
    /// <summary>The structure of the layout running on this thread, which line placement reads to split a
    /// run where the structural owner of its glyphs changes. Set by <see cref="TextLayout"/> around a layout.</summary>
    [ThreadStatic] internal static DocumentStructure? Active;

    private HtmlToken[] tokens;
    private int[] parents;
    private int[] ends;
    private int[] holders;
    private StructureRole[] roles;
    private int count;

    /// <summary>The number of elements.</summary>
    internal int Count => count;

    /// <summary>The document title (the head's title text, normalised), or null.</summary>
    internal string? Title { get; private set; }

    /// <summary>The html element's language tag, or null when the document names none.</summary>
    internal string? Language { get; private set; }

    /// <summary>The number of img and svg figures that have no alt text and are not marked decorative.</summary>
    internal int FiguresWithoutAlt { get; private set; }

    // The title element of an svg, by element index: the alt text of an svg without aria-label.
    private List<(int Element, string Text)>? svgTitles;

    private DocumentStructure(int capacity)
    {
        tokens = ArrayPool<HtmlToken>.Shared.Rent(capacity);
        parents = ArrayPool<int>.Shared.Rent(capacity);
        ends = ArrayPool<int>.Shared.Rent(capacity);
        holders = ArrayPool<int>.Shared.Rent(capacity);
        roles = ArrayPool<StructureRole>.Shared.Rent(capacity);
    }

    internal HtmlToken Token(int element) => tokens[element];
    internal int Parent(int element) => parents[element];
    internal int End(int element) => ends[element];
    internal StructureRole Role(int element) => roles[element];

    /// <summary>The nearest element at or above <paramref name="element"/> with a structure role, or -1.</summary>
    internal int Holder(int element) => holders[element];

    /// <summary>The element whose start tag is at <paramref name="sourceOffset"/>, or -1.</summary>
    internal int Find(int sourceOffset)
    {
        if (sourceOffset < 0) return -1;
        int low = 0, high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var offset = tokens[middle].Offset;
            if (offset == sourceOffset) return middle;
            if (offset < sourceOffset) low = middle + 1; else high = middle - 1;
        }
        return -1;
    }

    /// <summary>The structural holder of the element at a glyph's source offset: -1 for none.</summary>
    internal int HolderAt(int sourceOffset)
    {
        var element = Find(sourceOffset);
        return element < 0 ? -1 : holders[element];
    }

    /// <summary>The alt text of a figure element: its alt attribute, an svg's aria-label or title; null without one.</summary>
    internal string? Alt(int element)
    {
        var token = tokens[element];
        if (token.Attributes is { } attributes)
        {
            if (token.Value == "img")
                return attributes.TryGetValue("alt", out var alt) ? alt : null;
            if (attributes.TryGetValue("aria-label", out var label) && !string.IsNullOrWhiteSpace(label)) return label;
        }
        if (svgTitles is not null)
            foreach (var (owner, text) in svgTitles)
                if (owner == element) return text;
        return null;
    }

    internal string? Attribute(int element, string name) =>
        tokens[element].Attributes is { } attributes && attributes.TryGetValue(name, out var value) ? value : null;

    /// <summary>Returns the rented arrays; the structure must not be used afterwards.</summary>
    internal void Release()
    {
        if (count < 0) return;
        Array.Clear(tokens, 0, count);
        ArrayPool<HtmlToken>.Shared.Return(tokens);
        ArrayPool<int>.Shared.Return(parents);
        ArrayPool<int>.Shared.Return(ends);
        ArrayPool<int>.Shared.Return(holders);
        ArrayPool<StructureRole>.Shared.Return(roles);
        tokens = []; parents = []; ends = []; holders = []; roles = [];
        count = -1;
    }

    private void Add(HtmlToken token, int parent, StructureRole role)
    {
        if (count == tokens.Length) Grow();
        tokens[count] = token;
        parents[count] = parent;
        ends[count] = count + 1;
        roles[count] = role;
        holders[count] = role != StructureRole.None ? count : parent < 0 ? -1 : holders[parent];
        count++;
    }

    private void Grow()
    {
        var size = checked(tokens.Length * 2);
        tokens = Resize(tokens, size, clear: true);
        parents = Resize(parents, size);
        ends = Resize(ends, size);
        holders = Resize(holders, size);
        roles = Resize(roles, size);
    }

    private T[] Resize<T>(T[] source, int size, bool clear = false)
    {
        var larger = ArrayPool<T>.Shared.Rent(size);
        Array.Copy(source, larger, count);
        if (clear) Array.Clear(source, 0, count);
        ArrayPool<T>.Shared.Return(source);
        return larger;
    }

    private readonly record struct Frame(string Name, int Element, bool CellList);

    /// <summary>Builds the structure of a collected token list.</summary>
    internal static DocumentStructure Build(List<HtmlToken> list, CancellationToken cancellation)
    {
        var structure = new DocumentStructure(Math.Max(16, list.Count / 4));
        var frames = new List<Frame>();
        var tables = 0; var cellLists = 0;
        StringBuilder? title = null;
        var inTitle = false;
        for (var t = 0; t < list.Count; t++)
        {
            if ((t & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            var token = list[t];
            if (token.Kind == HtmlTokenKind.Text)
            {
                if (inTitle) (title ??= new()).Append(Decoded(token));
                continue;
            }
            if (token.Kind == HtmlTokenKind.StyleText) continue;
            var name = token.Value;
            if (token.Kind == HtmlTokenKind.Start)
            {
                if (Top(frames) is { } open && HtmlDocumentReader.StartClosesParagraph(open, name, tables > 0 && cellLists == 0))
                    Close(structure, frames, ref tables, ref cellLists);
                if (Top(frames) is { } item && HtmlDocumentReader.StartClosesListItem(item, name))
                    Close(structure, frames, ref tables, ref cellLists);
                var element = -1;
                if (token.Offset >= 0)
                {
                    var role = RoleOf(token, name);
                    if (role == StructureRole.Figure && FigureNeedsAlt(token)) structure.FiguresWithoutAlt++;
                    element = structure.count;
                    structure.Add(token, NearestElement(frames), role);
                    if (name == "html" && token.Attributes is { } attributes && attributes.TryGetValue("lang", out var lang))
                        structure.Language = LanguageTag(lang);
                }
                if (name is "img" or "hr" or "br" or "meta" or "link" or "base" or "col" || token.SelfClosing) continue;
                if (name == "svg")
                {
                    t = SkipSvg(structure, list, t, element);
                    structure.Close(element);
                    continue;
                }
                var cellList = tables > 0 && (cellLists > 0 || HtmlDocumentReader.ListTag(name) && Top(frames) is "td" or "th");
                if (cellList) cellLists++;
                frames.Add(new(name, element, cellList));
                if (name == "table") tables++;
                if (name == "title") inTitle = true;
                continue;
            }
            if (Top(frames) is { } p && HtmlDocumentReader.EndClosesParagraph(p, name, tables > 0 && cellLists == 0))
                Close(structure, frames, ref tables, ref cellLists);
            if (Top(frames) is { } li && HtmlDocumentReader.EndClosesListItem(li, name))
                Close(structure, frames, ref tables, ref cellLists);
            if (Top(frames) == name)
            {
                if (name == "title") inTitle = false;
                Close(structure, frames, ref tables, ref cellLists);
            }
        }
        while (frames.Count > 0) Close(structure, frames, ref tables, ref cellLists);
        structure.Title = title is null ? null : NormaliseTitle(title);
        return structure;
    }

    // Text with its character references decoded, as the reader decodes it; undecodable text stays as written.
    private static string Decoded(HtmlToken token)
    {
        try { return HtmlTokens.Decode(token.Value, token.Offset); }
        catch (FactsPdfException) { return token.Value; }
    }

    private static string? Top(List<Frame> frames) => frames.Count > 0 ? frames[^1].Name : null;

    private static int NearestElement(List<Frame> frames)
    {
        for (var i = frames.Count - 1; i >= 0; i--)
            if (frames[i].Element >= 0) return frames[i].Element;
        return -1;
    }

    private static void Close(DocumentStructure structure, List<Frame> frames, ref int tables, ref int cellLists)
    {
        var frame = frames[^1];
        structure.Close(frame.Element);
        if (frame.Name == "table") tables--;
        if (frame.CellList) cellLists--;
        frames.RemoveAt(frames.Count - 1);
    }

    private void Close(int element)
    {
        if (element >= 0) ends[element] = count;
    }

    // An img or svg is a figure unless it is marked decorative (empty alt, presentation role, aria-hidden).
    private static StructureRole RoleOf(HtmlToken token, string name)
    {
        var role = StructureRoles.FromTag(name);
        var attributes = token.Attributes;
        // canvas, video and iframe are replaced boxes that draw nothing here: no content, so no Figure.
        if (name is "canvas" or "video" or "iframe") return StructureRole.Artifact;
        if (name is "img" or "svg")
        {
            if (attributes is not null && (attributes.TryGetValue("role", out var presentation) && presentation is "presentation" or "none"
                || attributes.TryGetValue("aria-hidden", out var hidden) && hidden == "true"
                || name == "img" && attributes.TryGetValue("alt", out var alt) && string.IsNullOrWhiteSpace(alt)))
                return StructureRole.Artifact;
            return StructureRole.Figure;
        }
        if (role == StructureRole.None && attributes is not null && name is not ("html" or "body" or "head"))
        {
            // An inline element with its own language or an abbreviation expansion needs a structure
            // element to carry the attribute.
            if (attributes.TryGetValue("lang", out var lang) && LanguageTag(lang) is not null) return StructureRole.Span;
            if (name == "abbr" && attributes.TryGetValue("title", out var expansion) && !string.IsNullOrWhiteSpace(expansion))
                return StructureRole.Span;
        }
        return role;
    }

    private static bool FigureNeedsAlt(HtmlToken token)
    {
        var attributes = token.Attributes;
        if (attributes is null) return true;
        if (token.Value == "img") return !attributes.TryGetValue("alt", out var alt) || string.IsNullOrWhiteSpace(alt);
        return !(attributes.TryGetValue("aria-label", out var label) && !string.IsNullOrWhiteSpace(label));
    }

    // Skips an svg's content, keeping the text of its first title child as the svg's alt text.
    private static int SkipSvg(DocumentStructure structure, List<HtmlToken> list, int start, int element)
    {
        var depth = 1;
        var inTitle = false;
        StringBuilder? title = null;
        var t = start + 1;
        for (; t < list.Count; t++)
        {
            var token = list[t];
            if (token.Kind == HtmlTokenKind.Text) { if (inTitle) (title ??= new()).Append(Decoded(token)); continue; }
            if (token.Kind == HtmlTokenKind.StyleText) continue;
            if (token.Kind == HtmlTokenKind.Start)
            {
                if (token.Value == "svg" && !token.SelfClosing) depth++;
                else if (token.Value == "title" && title is null && !token.SelfClosing) inTitle = true;
                continue;
            }
            if (token.Value == "title") inTitle = false;
            else if (token.Value == "svg" && --depth == 0) break;
        }
        if (element >= 0 && structure.roles[element] == StructureRole.Figure && title is not null && NormaliseTitle(title) is { } text)
        {
            (structure.svgTitles ??= []).Add((element, text));
            if (FigureNeedsAlt(structure.tokens[element])) structure.FiguresWithoutAlt--;
        }
        return Math.Min(t, list.Count - 1);
    }

    /// <summary>A BCP 47 style language tag the PDF Lang entry accepts, or null for an empty or malformed value.</summary>
    internal static string? LanguageTag(string? value)
    {
        if (value is null) return null;
        var tag = value.Trim();
        if (tag.Length is 0 or > 35) return null;
        var subtagLength = 0;
        for (var i = 0; i < tag.Length; i++)
        {
            var c = tag[i];
            if (c == '-')
            {
                if (subtagLength == 0) return null;
                subtagLength = 0;
                continue;
            }
            if (!char.IsAsciiLetterOrDigit(c) || ++subtagLength > 8) return null;
        }
        return subtagLength == 0 || !char.IsAsciiLetter(tag[0]) ? null : tag;
    }

    // The Info and XMP title: ASCII whitespace collapsed, controls removed, at most 1024 UTF-16 units
    // ending on a whole scalar. Null when nothing is left.
    private static string? NormaliseTitle(StringBuilder text)
    {
        var result = new StringBuilder(Math.Min(text.Length, 1024));
        var pendingSpace = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is ' ' or '\t' or '\n' or '\r' or '\f') { pendingSpace = result.Length > 0; continue; }
            if (char.IsControl(c)) continue;
            var unit = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
            if (unit == 1 && char.IsSurrogate(c)) continue;
            if (result.Length + unit + (pendingSpace ? 1 : 0) > 1024) break;
            if (pendingSpace) { result.Append(' '); pendingSpace = false; }
            result.Append(c);
            if (unit == 2) result.Append(text[++i]);
        }
        return result.Length == 0 ? null : result.ToString();
    }
}
