namespace FactsPDF;

internal enum TextAlignment { Left, Center, Right, Justify }
// Table-cell content placement within a measured whole row; not CSS inline baseline alignment.
internal enum CellVerticalAlign { Top, Middle, Bottom }
[Flags]
internal enum TextDecoration { None = 0, Underline = 1, LineThrough = 2, Overline = 4 }
internal static class TextDecorationCommands
{
    internal static int Count(TextDecoration decoration) =>
        System.Numerics.BitOperations.PopCount((uint)decoration);
}
internal enum TextDecorationPattern { Solid, Dashed, Dotted }
internal enum TextTransform { None, Uppercase, Lowercase, Capitalize }
// This is the renderer's bounded outside-list marker selection, not CSS counters.
internal enum ListMarkerKind { Disc, Circle, Square, Decimal, LowerAlpha, UpperAlpha, LowerRoman, UpperRoman, None }
internal readonly record struct Rgb(double R, double G, double B);
// Computed text style. Treat every published instance as immutable: the root style, a parent's
// style and any style stored in the document model or a run are shared, and adjacent runs with
// equal styles coalesce by value. The setters exist only so CSS computing can apply all of one
// element's text declarations to a single private copy (HtmlDocumentReader, CssStylesheets.Compute),
// instead of allocating a full copy per declaration. Nothing else may assign these properties.
internal sealed record TextStyle
{
    internal TextStyle(double fontSize, double lineHeight, Rgb color, TextAlignment alignment)
    {
        FontSize = fontSize;
        LineHeight = lineHeight;
        Color = color;
        Alignment = alignment;
    }

    public double FontSize { get; set; }
    public double LineHeight { get; set; }
    public Rgb Color { get; set; }
    public TextAlignment Alignment { get; set; }
    public double MarginBefore { get; set; }
    public double MarginAfter { get; set; }
    public bool BreakBefore { get; set; }
    public bool BreakAfter { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool KeepWithNext { get; set; }
    public TextDecoration Decoration { get; set; }
    public double? AbsoluteLineHeight { get; set; }
    public bool NoWrap { get; set; }
    public CssLength FirstLineIndent { get; set; }
    public double LetterSpacing { get; set; }
    public double WordSpacing { get; set; }
    public TextTransform Transform { get; set; }
    public bool BreakWords { get; set; }
    public bool PreserveLineBreaks { get; set; }
    public bool PreserveSpaces { get; set; }
    public Rgb? DecorationColor { get; set; }
    public TextDecorationPattern DecorationPattern { get; set; }
    public bool BreakAll { get; set; }
    public string? LinkUri { get; set; }
    public double? DecorationThickness { get; set; }
    public double? UnderlineOffset { get; set; }
    public TextAlignment? LastLineAlignment { get; set; }
    // Retain relative outside margins until containing-block content width is known.
    public CssLength? PercentMarginBefore { get; set; }
    public CssLength? PercentMarginAfter { get; set; }
}
// Diagnostic provenance is separate from TextStyle equality: adjacent identical
// computed styles must still coalesce regardless of their source elements.
internal sealed record TextRun(string Text, TextStyle Style, bool IsBreak = false, int SourceOffset = -1);
internal sealed record Paragraph(TextStyle Style, int SourceOffset = -1) : BlockChild
{
    // A paragraph is its own block child: true for text directly inside a block (or a list
    // marker's generated line), false for the one paragraph a p, pre or heading element holds.
    // Set when the reader adds it to its parent, so no wrapper object per paragraph is needed.
    public bool IsAnonymous { get; set; }

    // Sized exactly when the reader knows how many runs the paragraph element holds; a
    // one-run paragraph keeps its run in the list's inline slot and allocates no array.
    public Paragraph(TextStyle style, int sourceOffset, int runCapacity) : this(style, sourceOffset)
        => runs = new(runCapacity);

    // The runs are stored in the paragraph itself (an array and a count) instead of a separate
    // List object per paragraph; Runs returns them by reference so Add grows this paragraph's copy.
    private TextRunList runs;
    public ref TextRunList Runs => ref runs;
}

/// <summary>A paragraph's growable run list, stored inline in its <see cref="Paragraph"/>.</summary>
/// <remarks>
/// A lone run is kept in the list's own slot; only a second run allocates an array (2, then
/// doubling), so a one-run paragraph such as <c>&lt;p&gt;text&lt;/p&gt;</c> allocates no array.
/// </remarks>
internal struct TextRunList : IReadOnlyList<TextRun>
{
    // null (no runs), the single TextRun, or a TextRun[] holding every run.
    private object? items;
    private int count;

    internal TextRunList(int capacity) => items = capacity > 1 ? new TextRun[capacity] : null;

    public readonly int Count => count;

    /// <summary>How many runs fit before the list grows (0 before the first run, 1 for the inline slot).</summary>
    public readonly int Capacity => items is TextRun[] array ? array.Length : items is null ? 0 : 1;

    public readonly TextRun this[int index]
        => (uint)index < (uint)count ? At(items, index) : throw new ArgumentOutOfRangeException(nameof(index));

    private static TextRun At(object? items, int index)
        => items is TextRun[] array ? array[index] : (TextRun)items!;

    internal void Add(TextRun run)
    {
        if (items is TextRun[] array)
        {
            if (count == array.Length) Array.Resize(ref array, checked(array.Length * 2));
            array[count++] = run;
            items = array;
        }
        else if (count == 0)
        {
            items = run;
            count = 1;
        }
        else
        {
            items = new TextRun[] { (TextRun)items!, run };
            count = 2;
        }
    }

    internal readonly bool HasBreak()
    {
        for (var i = 0; i < count; i++)
            if (At(items, i).IsBreak) return true;
        return false;
    }

    public readonly Enumerator GetEnumerator() => new(items, count);
    readonly IEnumerator<TextRun> IEnumerable<TextRun>.GetEnumerator() => GetEnumerator();
    readonly System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    internal struct Enumerator(object? items, int count) : IEnumerator<TextRun>
    {
        private int index = -1;
        public readonly TextRun Current => At(items, index);
        readonly object System.Collections.IEnumerator.Current => Current;
        public bool MoveNext() => ++index < count;
        public void Reset() => index = -1;
        public readonly void Dispose() { }
    }
}
internal record PlacedText(string Text, double X, double Baseline, TextStyle Style, PdfFont? Font = null, double Width = 0)
{
    // -1: plain runs keep no offset, so they stay 64 bytes; see LocatedText.
    internal virtual int SourceOffset => -1;
}
// A decorated or linked run keeps its paragraph's source offset for the paint-stage
// failures only such runs can raise; `with` copies keep the derived type and offset.
internal sealed record LocatedText(string Text, double X, double Baseline, TextStyle Style, PdfFont? Font, double Width,
    int Offset) : PlacedText(Text, X, Baseline, Style, Font, Width)
{
    internal override int SourceOffset => Offset;
}
// SourceOffset is the list item's; it packs beside Kind, so a disc stays 72 bytes.
internal sealed record PlacedDisc(double X, double Y, double Radius, Rgb Color, ListMarkerKind Kind = ListMarkerKind.Disc,
    int SourceOffset = -1);
internal sealed record PlacedImage(PdfImage Image, double X, double Top, double Width, double Height);
internal sealed class LayoutPage
{
    public List<PlacedText> Runs { get; } = [];
    public List<PaintedBox> PaintBoxes { get; } = [];
    public List<PlacedDisc> Discs { get; } = [];
    public List<PlacedImage> Images { get; } = [];
}

internal sealed record PaintedBox(double X, double Top, double Width, double Height,
    BoxStyle Style, Rgb TextColor, int Order,
    bool IsFirstFragment = true, bool IsLastFragment = true, int SourceOffset = -1)
{
    // A page fragment can be shorter than its specified border. Use the same
    // clipped geometry for serialization and display-command accounting.
    internal double TopBorderHeight => IsFirstFragment
        ? Math.Min(Math.Max(0, Height), Style.BorderTop.EffectiveWidth) : 0;
    internal double BottomBorderHeight => IsLastFragment
        ? Math.Min(Math.Max(0, Height), Style.BorderBottom.EffectiveWidth) : 0;

    // Counts the optional outline as a single PDF stroke command.
    // Exactly the nonzero rectangles emitted by PdfPaintSerializer.Rect.
    // An empty background or vertical edge of a zero-height box costs 0.
    public int CommandCount
    {
        get
        {
            if (Style.HasRoundedCorners)
                return Width > 0 && Height > 0
                    ? (Style.BackgroundColor.HasValue ? 1 : 0)
                      + (Style.BorderLeft.EffectiveWidth > 0 ? 1 : 0)
                    : 0;
            var outline = Width > 0 && Height > 0 && Style.Outline.EffectiveWidth > 0 ? 1 : 0;
            var top = TopBorderHeight;
            var bottom = BottomBorderHeight;
            var middleHeight = Math.Max(0, Height - top - bottom);
            return outline + (Width > 0 && Height > 0 && Style.BackgroundColor.HasValue ? 1 : 0)
                + (Width > 0 && top > 0 ? 1 : 0)
                + (Width > 0 && bottom > 0 ? 1 : 0)
                + (middleHeight > 0 && Style.BorderLeft.EffectiveWidth > 0 ? 1 : 0)
                + (middleHeight > 0 && Style.BorderRight.EffectiveWidth > 0 ? 1 : 0);
        }
    }
}

// M5 retains the box tree; M6 computes geometry; M7 paints single-page
// borders/backgrounds; M8 slices continued decorations across page fragments.
internal readonly record struct CssLength(double Value, bool IsPercent = false)
{
    internal double Resolve(double containingWidth) => IsPercent ? containingWidth * Value / 100 : Value;
}

// Specified values are retained until the containing block's content width is known.
// All properties in BoxStyle are non-inherited by default.
// The pattern affects PDF strokes, while the existing Solid bit continues to
// distinguish painted borders from CSS none without changing legacy boxes.
internal enum BorderPattern { Solid, Dashed, Dotted }
internal readonly record struct BorderEdge(double Width, bool Solid, Rgb? Color)
{
    internal BorderPattern Pattern { get; init; } = BorderPattern.Solid;
    internal static BorderEdge Initial => new(2.25, false, null); // CSS medium=3px
    internal double EffectiveWidth => Solid ? Width : 0;
}

// Computed box style. Treat every published instance as immutable: Initial, a parent's box and any
// box stored in the document model are shared. The setters exist only so CSS computing can apply
// all of one element's declarations to a single private copy (CssStylesheets.Compute), instead of
// allocating a full copy per declaration. Nothing else may assign these properties.
internal sealed record BoxStyle
{
    // The CSS initial box, shared by every element without box declarations and by every
    // CSS-wide initial/unset keyword. Never mutate it.
    internal static readonly BoxStyle Initial = new();

    public CssLength? Width { get; set; }
    public CssLength PaddingTop { get; set; }
    public CssLength PaddingRight { get; set; }
    public CssLength PaddingBottom { get; set; }
    public CssLength PaddingLeft { get; set; }

    // Report panel vertical margins are outside paint and have no general CSS
    // collapsing behaviour. Existing paragraph/heading vertical margins remain
    // in TextStyle, where their historical overlap rules are preserved.
    public CssLength MarginTop { get; set; }
    public CssLength MarginBottom { get; set; }
    // Outside the paragraph/heading border box; percentages use the containing width.
    public CssLength MarginLeft { get; set; }
    public CssLength MarginRight { get; set; }
    // Auto is distinguished from an explicit zero without changing legacy lengths.
    public bool MarginLeftAuto { get; set; }
    public bool MarginRightAuto { get; set; }
    // Explicit width includes padding/borders when requested; defaults unchanged.
    public bool BorderBoxSizing { get; set; }
    // Only ordinary block display:none is modelled. The parser removes the
    // complete validated subtree before any PDF layout or paint commands.
    public bool DisplayNone { get; set; }
    // Only td/th cells consume this property; rows needing fragmentation reject middle/bottom.
    public CellVerticalAlign VerticalAlign { get; set; } = CellVerticalAlign.Top;
    // Unlike painted box sides, list-style-type is inherited through li and
    // other containers. The ol/ul HTML defaults are applied in CSS computing.
    public ListMarkerKind MarkerKind { get; set; } = ListMarkerKind.Disc;
    // Inherited list marker placement. Outside retains all previous PDF geometry;
    // inside reserves inline advance on the first text line only.
    public bool MarkerInside { get; set; }
    // Opt-in width constraints. Percentages resolve against the containing
    // content width; the constrained measure follows box-sizing.
    public CssLength? MinWidth { get; set; }
    public CssLength? MaxWidth { get; set; }
    // Nonnegative minimum content-box height, or border-box with box-sizing.
    // Only ordinary report panels consume it during PDF page placement.
    public CssLength? MinHeight { get; set; }
    // Only explicitly bound images consume a physical maximum height.
    public CssLength? MaxHeight { get; set; }
    // Fixed, non-fragmented report panels reject overflow instead of clipping.
    public CssLength? Height { get; set; }
    // Explicit before/after page boundaries for ordinary report panels.
    // These never inherit unless the winning CSS keyword is inherit.
    public bool BreakBefore { get; set; }
    public bool BreakAfter { get; set; }
    // A bounded, top-level flat report card can be preflighted and moved
    // intact rather than letting its paragraphs split between PDF pages.
    public bool AvoidBreakInside { get; set; }
    public Rgb? BackgroundColor { get; set; } // null: transparent
    // Retain the currentColor token when background:inherit is authored on a
    // child: its actual ink resolves against the receiving element's colour.
    // The renderer still consumes only the final BackgroundColor RGB value.
    public bool BackgroundUsesCurrentColor { get; set; }
    // Rounded-corner shorthand computes TL / TR / BR / BL physical radii.
    // CornerRadius keeps the original TL member so one-value reports retain
    // their source layout and exact legacy PDF bytes.
    public double CornerRadius { get; set; }
    public double CornerTopRightRadius { get; set; }
    public double CornerBottomRightRadius { get; set; }
    public double CornerBottomLeftRadius { get; set; }
    public bool HasRoundedCorners => CornerRadius > 0 || CornerTopRightRadius > 0 ||
        CornerBottomRightRadius > 0 || CornerBottomLeftRadius > 0;
    // This non-inherited outline is painted outside the border box.
    public BorderEdge Outline { get; set; } = BorderEdge.Initial;
    public double OutlineOffset { get; set; }
    public BorderEdge BorderTop { get; set; } = BorderEdge.Initial;
    public BorderEdge BorderRight { get; set; } = BorderEdge.Initial;
    public BorderEdge BorderBottom { get; set; } = BorderEdge.Initial;
    public BorderEdge BorderLeft { get; set; } = BorderEdge.Initial;
    // Ordinary fill and border paint can anchor a box's leading layout across
    // page breaks. An outside outline is ink only: it must not consume padding
    // or change the page on which a panel's first text is placed.
    public bool HasLayoutPaint => BackgroundColor.HasValue ||
        BorderTop.EffectiveWidth > 0 || BorderRight.EffectiveWidth > 0 ||
        BorderBottom.EffectiveWidth > 0 || BorderLeft.EffectiveWidth > 0;
    public bool HasPaint => HasLayoutPaint || Outline.EffectiveWidth > 0;
}

internal abstract record BlockChild;

internal sealed record BlockNode(string Name, TextStyle Style, BoxStyle BoxStyle, int SourceOffset) : BlockChild
{
    public BlockChildList Children { get; } = new();
    // Bounded HTML ordered-list sequence metadata. These are parsed values,
    // never generated child nodes; a tree may be laid out repeatedly.
    // Packed into one int, so a block node is 56 bytes instead of 64: the reader sets a start
    // only on an ol and a value only on an li, ordinals are at most ListMarkerTypes.MaxOrdinal
    // (below 2^20), and the reversed flag is one more bit.
    private readonly int list;
    private const int OrdinalMask = (1 << 20) - 1, StartFlag = 1 << 20, ValueFlag = 1 << 21, ReversedFlag = 1 << 22;
    public int? ListStart { get => Ordinal(StartFlag); init => list = WithOrdinal(StartFlag, value); }
    public int? ListValue { get => Ordinal(ValueFlag); init => list = WithOrdinal(ValueFlag, value); }
    public bool ListReversed { get => (list & ReversedFlag) != 0; init => list = value ? list | ReversedFlag : list & ~ReversedFlag; }

    private int? Ordinal(int flag) => (list & flag) != 0 ? list & OrdinalMask : null;

    private int WithOrdinal(int flag, int? ordinal)
    {
        if (ordinal is null) return (list & flag) != 0 ? list & ReversedFlag : list;
        if (ordinal is < 1 or > ListMarkerTypes.MaxOrdinal)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if ((list & (StartFlag | ValueFlag) & ~flag) != 0)
            throw new InvalidOperationException("A block node holds either a list start or a list item value, not both.");
        return (list & ReversedFlag) | flag | ordinal.Value;
    }
}


/// <summary>
/// The children of a block or of the document root. The first child is kept in a slot of the list
/// itself and only later children go to an array, so a paragraph element (which holds just its own
/// paragraph) allocates no array; growth past the slot follows List's (4, then doubling).
/// </summary>
internal sealed class BlockChildList : IReadOnlyList<BlockChild>
{
    private BlockChild? first;
    private BlockChild[]? rest;
    private int count;

    public int Count => count;

    /// <summary>The first-child slot plus the length of the array for later children.</summary>
    public int Capacity => 1 + (rest?.Length ?? 0);

    public BlockChild this[int index]
        => (uint)index < (uint)count
            ? (index == 0 ? first! : rest![index - 1])
            : throw new ArgumentOutOfRangeException(nameof(index));

    internal void Add(BlockChild child)
    {
        if (count == 0) first = child;
        else
        {
            var slot = count - 1;
            if (rest is null || slot == rest.Length)
                Array.Resize(ref rest, rest is null ? 4 : checked(rest.Length * 2));
            rest[slot] = child;
        }
        count++;
    }

    internal void RemoveAt(int index)
    {
        if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
        if (index == 0)
        {
            first = count > 1 ? rest![0] : null;
            index = 1;
        }
        // Shift the later children down over the removed one and clear the vacated slot.
        for (var i = index; i < count - 1; i++) rest![i - 1] = rest[i];
        if (count > 1) rest![count - 2] = null!;
        count--;
    }

    /// <summary>
    /// Whether every child matches, by index: LINQ's <c>All</c> takes a span path over a
    /// <c>List</c> but would box this list's enumerator, once per block laid out.
    /// </summary>
    internal bool All(Func<BlockChild, bool> predicate)
    {
        for (var i = 0; i < count; i++)
            if (!predicate(this[i])) return false;
        return true;
    }

    public Enumerator GetEnumerator() => new(this);
    IEnumerator<BlockChild> IEnumerable<BlockChild>.GetEnumerator() => GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    internal struct Enumerator(BlockChildList list) : IEnumerator<BlockChild>
    {
        private int index = -1;
        public readonly BlockChild Current => list[index];
        readonly object System.Collections.IEnumerator.Current => Current;
        public bool MoveNext() => ++index < list.count;
        public void Reset() => index = -1;
        public readonly void Dispose() { }
    }
}

internal sealed record ImageNode(PdfImage Image, TextStyle Style, BoxStyle BoxStyle,
    int SourceOffset, string? Alt) : BlockChild;

internal sealed class DocumentRoot
{
    public BlockChildList Children { get; } = new();
    public bool HasLists { get; set; }
}
