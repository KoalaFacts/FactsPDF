namespace FactsPDF;

internal enum TextAlignment { Left, Center, Right, Justify }
// Table-cell content placement within a measured whole row; not CSS inline baseline alignment.
// DefaultMiddle is the user-agent sheet's middle: it centres a cell in a row that stays whole and, since a
// split row cannot centre, leaves the cell at the top there; an authored middle fails in a split row.
internal enum CellVerticalAlign { Top, Middle, Bottom, DefaultMiddle }
[Flags]
internal enum TextDecoration { None = 0, Underline = 1, LineThrough = 2, Overline = 4 }
internal static class TextDecorationCommands
{
    internal static int Count(TextDecoration decoration) =>
        System.Numerics.BitOperations.PopCount((uint)decoration);
}
internal enum TextDecorationPattern { Solid, Dashed, Dotted }
internal enum InlineAlignKind : byte { Baseline, Middle, Top, Bottom, TextTop, TextBottom, Shift }
// An atomic inline's vertical-align. Shift raises the box's bottom above the baseline by an
// absolute length in points (negative lowers it), or by a percentage of the line-height.
internal readonly record struct InlineVerticalAlign(InlineAlignKind Kind, double Shift = 0, bool IsPercent = false)
{
    internal static bool TryParse(string value, out InlineVerticalAlign result, double? elementFontSize = null,
        double? rootFontSize = null)
    {
        result = value switch
        {
            "baseline" => new(InlineAlignKind.Baseline),
            "middle" => new(InlineAlignKind.Middle),
            "top" => new(InlineAlignKind.Top),
            "bottom" => new(InlineAlignKind.Bottom),
            "text-top" => new(InlineAlignKind.TextTop),
            "text-bottom" => new(InlineAlignKind.TextBottom),
            _ => default,
        };
        if (value is "baseline" or "middle" or "top" or "bottom" or "text-top" or "text-bottom") return true;
        var negative = value.StartsWith('-');
        var magnitude = negative || value.StartsWith('+') ? value[1..] : value;
        if (magnitude.Length == 0 || magnitude[0] is '-' or '+'
            || !CssBoxValues.TryLength(magnitude, out var length, out _, rootFontSize, allowRem: true,
                elementFontSize, allowEm: true))
            return false;
        result = new(InlineAlignKind.Shift, negative ? -length.Value : length.Value, length.IsPercent);
        return true;
    }
}
internal enum TextTransform { None, Uppercase, Lowercase, Capitalize }
internal enum TextVisibility : byte { Visible, Hidden, Collapse }
internal enum OverflowKind : byte { Visible, Hidden, Clip, Scroll, Auto }
// This is the renderer's bounded outside-list marker selection, not CSS counters.
// Disclosure is the first summary of a details element: its user-agent marker, a triangle that
// points right while the details is closed and down while it is open. Author CSS can only replace it
// (list-style-type:none removes it); it is never written as a type name.
internal enum ListMarkerKind { Disc, Circle, Square, Decimal, LowerAlpha, UpperAlpha, LowerRoman, UpperRoman, None, Disclosure }
// Each channel is held as one byte (n/255): every colour CSS gives FactsPDF is a byte per channel,
// and A is the CSS alpha quantised to one byte, as Chrome's PDF backend stores paint alpha. Four
// bytes keep a colour smaller than the three doubles it replaced, so alpha costs no style or
// placed-marker allocation. A of 1 is opaque and paints through the legacy opaque PDF path byte
// for byte; 0 paints nothing.
internal readonly record struct Rgb
{
    private readonly byte r, g, b, a;

    internal Rgb(double r, double g, double b, double a = 1)
    {
        this.r = Quantise(r);
        this.g = Quantise(g);
        this.b = Quantise(b);
        this.a = Quantise(a);
    }

    internal double R => r / 255d;
    internal double G => g / 255d;
    internal double B => b / 255d;
    internal double A => a / 255d;
    internal bool IsInvisible => a == 0;

    private static byte Quantise(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);
}
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

    /// <summary>
    /// The <see cref="LineHeight"/> of line-height:normal, the CSS initial value and the user-agent
    /// default: the line box takes its height from the first font's metrics (see TextLines).
    /// A specified unitless line-height is from 1 to 10, so a negative multiplier is never authored.
    /// </summary>
    internal const double NormalLineHeight = -1;

    public double FontSize { get; set; }
    public double LineHeight { get; set; }
    /// <summary>Whether line-height computes to normal (neither a multiplier nor a length).</summary>
    internal bool HasNormalLineHeight => AbsoluteLineHeight is null && LineHeight < 0;
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
    /// <summary>word-break:keep-all: no break between letters, ideographs, kana or Hangul syllables.</summary>
    public bool KeepAll { get => (flow & 1) != 0; set => Bit(1, value); }
    /// <summary>hyphens:none: a soft hyphen (U+00AD) is no line break opportunity; manual, the initial value, breaks there and shows a hyphen.</summary>
    public bool NoHyphens { get => (flow & 2) != 0; set => Bit(2, value); }
    /// <summary>white-space:break-spaces (white-space-collapse:break-spaces): preserved spaces never hang and each may wrap.</summary>
    public bool BreakSpaces { get => (flow & 4) != 0; set => Bit(4, value); }
    /// <summary>tab-size: a count of space advances, or, stored negated, a length in points.</summary>
    public float TabSize { get; set; } = 8;
    internal bool TabSizeIsLength => TabSize < 0;
    /// <summary>line-break; Normal is also auto.</summary>
    public LineBreakStrictness LineBreak { get => (LineBreakStrictness)((flow >> 3) & 3); set => flow = (byte)((flow & ~(3 << 3)) | ((int)value << 3)); }
    /// <summary>visibility (inherited): hidden and collapse keep the layout and paint nothing.</summary>
    public TextVisibility Visibility { get => (TextVisibility)((flow >> 5) & 3); set => flow = (byte)((flow & ~(3 << 5)) | ((int)value << 5)); }
    // The line breaking and visibility switches above, packed into one byte: a style is allocated per element.
    private byte flow;
    private void Bit(int bit, bool on) => flow = (byte)(on ? flow | bit : flow & ~bit);
    /// <summary>word-break:break-word, which wraps like overflow-wrap:anywhere whatever overflow-wrap computes to.</summary>
    public bool BreakWordsByWordBreak { get; set; }
    /// <summary>overflow-wrap:anywhere: wraps like break-word, but its breaks also count for min-content.</summary>
    public bool BreakAnywhere { get; set; }
    /// <summary>Whether a token too wide for a fresh line may break between glyphs (overflow-wrap:break-word/anywhere or word-break:break-word).</summary>
    public bool EmergencyBreak => BreakWords || BreakWordsByWordBreak;
    public string? LinkUri { get; set; }
    public double? DecorationThickness { get; set; }
    public double? UnderlineOffset { get; set; }
    public TextAlignment? LastLineAlignment { get; set; }
    /// <summary>
    /// Computed CSS orphans and widows, or 0 for the initial value 2 (see
    /// <see cref="LineBreakControl"/>), so the default style needs no assignment.
    /// </summary>
    public ushort Orphans { get; set; }
    public ushort Widows { get; set; }
    // Retain relative outside margins until containing-block content width is known.
    public CssLength? PercentMarginBefore { get; set; }
    public CssLength? PercentMarginAfter { get; set; }
    /// <summary>border-collapse:collapse (inherited); only a table's own computed value is read.</summary>
    public bool CollapseBorders { get; set; }
}
// Diagnostic provenance is separate from TextStyle equality: adjacent identical
// computed styles must still coalesce regardless of their source elements.
internal record TextRun(string Text, TextStyle Style, bool IsBreak = false, int SourceOffset = -1);
// An img or svg with display:inline (the default): an atomic inline box in its paragraph's line
// boxes. Style is the parent inline's style, whose baseline, x-height and link the box aligns to
// and inherits; the image's own box style is on Image.
internal sealed record InlineImageRun(ImageNode Image, TextStyle Style)
    : TextRun("", Style, false, Image.SourceOffset);
internal sealed record Paragraph(TextStyle Style, int SourceOffset = -1) : BlockChild
{
    private const uint AnonymousBit = 0x8000_0000;

    // SourceOffset + 1 in the low 31 bits (a source offset is a string index, so at most
    // int.MaxValue - 1, and -1 means none) and IsAnonymous in the top bit, kept in the run list's
    // spare four bytes: an int and a bool beside the list cost a paragraph 48 bytes, this 40.
    private TextRunList runs = new() { OwnerBits = checked((uint)(SourceOffset + 1)) };

    public int SourceOffset
    {
        get => (int)(runs.OwnerBits & ~AnonymousBit) - 1;
        init => runs.OwnerBits = (runs.OwnerBits & AnonymousBit) | checked((uint)(value + 1));
    }

    // A paragraph is its own block child: true for text directly inside a block (or a list
    // marker's generated line), false for the one paragraph a p, pre or heading element holds.
    // Set when the reader adds it to its parent, so no wrapper object per paragraph is needed.
    public bool IsAnonymous
    {
        get => (runs.OwnerBits & AnonymousBit) != 0;
        set => runs.OwnerBits = value ? runs.OwnerBits | AnonymousBit : runs.OwnerBits & ~AnonymousBit;
    }

    // Sized exactly when the reader knows how many runs the paragraph element holds; a
    // one-run paragraph keeps its run in the list's inline slot and allocates no array.
    public Paragraph(TextStyle style, int sourceOffset, int runCapacity) : this(style, sourceOffset)
        => runs = new(runCapacity) { OwnerBits = runs.OwnerBits };

    // The runs are stored in the paragraph itself (an array and a count) instead of a separate
    // List object per paragraph; Runs returns them by reference so Add grows this paragraph's copy.
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

    // Not part of the list: four bytes the owning Paragraph keeps here (its source offset and
    // anonymous flag) because the struct pads an object and an int to 16 bytes anyway.
    internal uint OwnerBits;

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
// SourceOffset is the list item's; it packs beside Kind, so a disc stays 56 bytes.
internal sealed record PlacedDisc(double X, double Y, double Radius, Rgb Color, ListMarkerKind Kind = ListMarkerKind.Disc,
    int SourceOffset = -1);
// LinkUri: an inline image inside a link, whose box becomes the link annotation's rectangle.
// Decoration: an inline image's own padding, border and background, painted just before it, with
// X and Top relative to the image's (so moving the image moves it too).
internal sealed record PlacedImage(PdfImage? Image, double X, double Top, double Width, double Height,
    SvgGraphic? Vector = null, string? LinkUri = null, int SourceOffset = -1, PaintedBox? Decoration = null,
    ObjectPlacement? Placement = null)
{
    internal int CommandCount => Vector?.CommandCount ?? 1;
}
/// <summary>
/// A rectangle (the padding box of an overflow:hidden, clip, scroll or auto box, in top-origin page points)
/// that clips what it holds: the page's runs, list markers and images in the given index ranges and the
/// paint boxes that name it. Parent is the enclosing region's number plus one, or 0.
/// </summary>
internal sealed record ClipRegion(double X, double Top, double Width, double Height, bool ClipX, bool ClipY,
    int RunStart, int RunEnd, int DiscStart, int DiscEnd, int ImageStart, int ImageEnd)
{
    public int Parent { get; set; }
}

internal sealed class LayoutPage
{
    // Created by the first clipped box, so a page without one allocates no list.
    private List<ClipRegion>? clips;
    private static readonly List<ClipRegion> NoClips = [];
    public List<ClipRegion> Clips => clips ?? NoClips;
    internal List<ClipRegion> WritableClips => clips ??= [];
    public List<PlacedText> Runs { get; } = [];
    public List<PaintedBox> PaintBoxes { get; } = [];
    public List<PlacedDisc> Discs { get; } = [];
    public List<PlacedImage> Images { get; } = [];
}

internal sealed record PaintedBox(double X, double Top, double Width, double Height,
    BoxStyle Style, Rgb TextColor, int Order,
    bool IsFirstFragment = true, bool IsLastFragment = true, int SourceOffset = -1, int Clip = 0)
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
    public int CommandCount => CountCommands(Width, Height, Style, IsFirstFragment, IsLastFragment);

    // The same count for a box that has not been allocated, so a caller can charge a
    // paint-free box and skip creating it.
    internal static int CountCommands(double width, double height, BoxStyle style,
        bool isFirstFragment = true, bool isLastFragment = true)
    {
        if (style.HasRoundedCorners)
            return width > 0 && height > 0
                ? (style.BackgroundColor.HasValue ? 1 : 0)
                  + (style.BorderLeft.EffectiveWidth > 0 && !style.BorderLeft.Transparent ? 1 : 0)
                : 0;
        var outline = width > 0 && height > 0 && style.Outline.EffectiveWidth > 0 ? 1 : 0;
        var top = isFirstFragment ? Math.Min(Math.Max(0, height), style.BorderTop.EffectiveWidth) : 0;
        var bottom = isLastFragment ? Math.Min(Math.Max(0, height), style.BorderBottom.EffectiveWidth) : 0;
        var middleHeight = Math.Max(0, height - top - bottom);
        // A groove is two bands, so it costs two rectangles.
        static int Edge(BorderEdge edge) => edge.Pattern == BorderPattern.Groove ? 2 : 1;
        return outline + (width > 0 && height > 0 && style.BackgroundColor.HasValue ? 1 : 0)
            + (width > 0 && top > 0 && !style.BorderTop.Transparent ? Edge(style.BorderTop) : 0)
            + (width > 0 && bottom > 0 && !style.BorderBottom.Transparent ? Edge(style.BorderBottom) : 0)
            + (middleHeight > 0 && style.BorderLeft.EffectiveWidth > 0 && !style.BorderLeft.Transparent ? Edge(style.BorderLeft) : 0)
            + (middleHeight > 0 && style.BorderRight.EffectiveWidth > 0 && !style.BorderRight.Transparent ? Edge(style.BorderRight) : 0);
    }
}

// M5 retains the box tree; M6 computes geometry; M7 paints single-page
// borders/backgrounds; M8 slices continued decorations across page fragments.
// A length in points, or (IsPercent) a percentage of the containing block plus Fixed points, which only
// a calc() sum of a percentage and a length produces.
internal readonly record struct CssLength(double Value, bool IsPercent = false, double Fixed = 0)
{
    internal double Resolve(double containingWidth) => IsPercent ? containingWidth * Value / 100 + Fixed : Value;

    /// <summary>Whether the length is a percentage plus a fixed length (see calc()).</summary>
    internal bool IsMixed => IsPercent && Fixed != 0;
}

// Specified values are retained until the containing block's content width is known.
// All properties in BoxStyle are non-inherited by default.
// The pattern affects PDF strokes, while the existing Solid bit continues to
// distinguish painted borders from CSS none without changing legacy boxes.
internal enum BackgroundClip { BorderBox, PaddingBox, ContentBox }
// Groove is the user-agent border of fieldset (2px groove): two bands of one colour, which only that
// element's default can produce (author border-style values stay solid, dashed, dotted and none).
internal enum BorderPattern { Solid, Dashed, Dotted, Groove }
internal readonly record struct BorderEdge(double Width, bool Solid, Rgb? Color)
{
    internal BorderPattern Pattern { get; init; } = BorderPattern.Solid;
    // border-color: transparent keeps the edge's width in layout but paints
    // nothing; the background (painted to the border box) shows through.
    internal bool Transparent { get; init; }
    // border-style:hidden paints nothing like none, but in the collapsing border model it
    // suppresses every other border on the same edge.
    internal bool Hidden { get; init; }
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
    // Only img and svg consume it: display:block lays them out as block boxes; the initial
    // inline (and inline-block, identical for a replaced element) puts them in line boxes.
    public bool DisplayBlock { get; set; }
    // Only td/th cells consume this property; rows needing fragmentation reject middle/bottom.
    public CellVerticalAlign VerticalAlign { get; set; } = CellVerticalAlign.Top;
    // vertical-align of an inline img or svg within its line box.
    public InlineVerticalAlign InlineAlign { get; set; }
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
    // overflow-x and overflow-y as declared; see ClipsX and ClipsY for what they compute to.
    public OverflowKind OverflowX { get; set; }
    public OverflowKind OverflowY { get; set; }
    // text-overflow:ellipsis (the initial clip ends a line of overflowing text at the box edge).
    public bool TextOverflowEllipsis { get; set; }
    // A visible axis computes to auto beside a scrolling or hidden one, and a clip axis to hidden, so only
    // clip beside visible leaves the other axis alone (CSS Overflow 3, section 3).
    public bool ClipsX => OverflowX != OverflowKind.Visible || OverflowY is OverflowKind.Hidden or OverflowKind.Scroll or OverflowKind.Auto;
    // An axis that computes to auto draws a scrollbar when its content overflows, which is not supported.
    public bool AutoX => OverflowX == OverflowKind.Auto || OverflowX == OverflowKind.Visible && OverflowY is OverflowKind.Hidden or OverflowKind.Auto;
    public bool AutoY => OverflowY == OverflowKind.Auto || OverflowY == OverflowKind.Visible && OverflowX is OverflowKind.Hidden or OverflowKind.Auto;
    public bool Radius => HasRoundedCorners;
    public bool ClipsY => OverflowY != OverflowKind.Visible || OverflowX is OverflowKind.Hidden or OverflowKind.Scroll or OverflowKind.Auto;
    // A bounded, top-level flat report card can be preflighted and moved
    // intact rather than letting its paragraphs split between PDF pages.
    public bool AvoidBreakInside { get; set; }
    public Rgb? BackgroundColor { get; set; } // null: transparent
    // The box edge the background colour is painted to (background-clip); not inherited.
    // The rarely set properties share one lazily created reference, so a box that sets none of them costs
    // eight bytes: background-clip, object-fit and object-position, table-layout and the counter lists.
    private sealed record Rare(BackgroundClip Clip, ObjectPlacement Placement, bool FixedTable, CounterOps Counters)
    {
        internal static readonly Rare Default = new(BackgroundClip.BorderBox, ObjectPlacement.Initial, false, CounterOps.None);
    }
    private Rare? rare;
    public BackgroundClip BackgroundClip { get => (rare ?? Rare.Default).Clip; set => rare = (rare ?? Rare.Default) with { Clip = value }; }
    // object-fit and object-position of an img; not inherited.
    public ObjectPlacement ObjectPlacement { get => (rare ?? Rare.Default).Placement; set => rare = (rare ?? Rare.Default) with { Placement = value }; }
    // table-layout:fixed on a table: columns come from the table width, the col widths and the first row.
    public bool FixedTableLayout { get => (rare ?? Rare.Default).FixedTable; set => rare = (rare ?? Rare.Default) with { FixedTable = value }; }
    // counter-reset, counter-increment and counter-set; not inherited.
    public CounterOps Counters { get => (rare ?? Rare.Default).Counters; set => rare = (rare ?? Rare.Default) with { Counters = value }; }
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
    // display:flex and the flex container and item properties, one shared reference so a box
    // without any of them keeps the initial instance and costs no extra object.
    public FlexStyle Flex { get; set; } = FlexStyle.Initial;
    public bool IsFlexContainer => Flex.Display == BoxDisplay.Flex;
    public bool IsGridContainer => Flex.Display == BoxDisplay.Grid;
    // A flex or grid container: its children (inline ones and text runs too) are items.
    public bool HasItems => Flex.Display != BoxDisplay.Block;
    public bool HasLayoutPaint => BackgroundColor.HasValue ||
        BorderTop.EffectiveWidth > 0 || BorderRight.EffectiveWidth > 0 ||
        BorderBottom.EffectiveWidth > 0 || BorderLeft.EffectiveWidth > 0;
    public bool HasPaint => HasLayoutPaint || Outline.EffectiveWidth > 0;
}

internal enum BoxDisplay { Block, Flex, Grid }
// Main-axis distribution of a flex line or of grid columns. Normal (also stretch) packs flex items
// at the start and stretches auto grid tracks.
internal enum FlexJustify { Normal, Start, End, Center, SpaceBetween, SpaceAround, SpaceEvenly }
// Cross-axis placement of a flex item; Stretch is also the computed normal.
internal enum FlexAlign { Stretch, Start, End, Center, Baseline }

// Computed display, flex and grid properties. None is inherited. Treat every published instance as
// immutable, like BoxStyle: CSS computing assigns only its own private copy.
internal sealed record FlexStyle
{
    internal static readonly FlexStyle Initial = new();

    public BoxDisplay Display { get; set; }
    public FlexJustify JustifyContent { get; set; }
    public FlexAlign AlignItems { get; set; }
    // null is align-self:auto, which takes the container's align-items.
    public FlexAlign? AlignSelf { get; set; }
    public double Grow { get; set; }
    public double Shrink { get; set; } = 1;
    // null is flex-basis:auto; BasisContent is flex-basis:content.
    public CssLength? Basis { get; set; }
    public bool BasisContent { get; set; }
    // null is gap normal, which is zero in a flex container.
    public CssLength? ColumnGap { get; set; }
    public CssLength? RowGap { get; set; }
    public int Order { get; set; }
    // Explicit grid columns; null is grid-template-columns:none (one implicit auto column).
    public GridTrack[]? GridColumns { get; set; }
    public FlexAlign JustifyItems { get; set; }
    // null is justify-self:auto, which takes the grid container's justify-items.
    public FlexAlign? JustifySelf { get; set; }
}

internal abstract record BlockChild;

// The single flex line of a flex container, standing in for the container's children in block
// layout. Never part of the parsed tree.
internal sealed record FlexLine(BlockNode Container, double VerticalEdges) : BlockChild;

// The rows of a grid container, standing in for the container's children in block layout.
internal sealed record GridRows(BlockNode Container) : BlockChild;

internal sealed record BlockNode(string Name, TextStyle Style, BoxStyle BoxStyle, int SourceOffset) : BlockChildList
{
    /// <summary>The node's children: the node is its own child list, so no separate list object.</summary>
    public BlockChildList Children => this;

    // The synthesized PrintMembers would print Children, which is this node, and recurse forever.
    protected override bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append("Name = ").Append(Name).Append(", SourceOffset = ").Append(SourceOffset)
            .Append(", Count = ").Append(Count);
        return true;
    }
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
/// The children of a block or of the document root. A lone child is kept in the list's own field
/// and only a second child allocates an array (4, then doubling, like List), so a paragraph element
/// (which holds just its own paragraph) allocates no array. One field for both shapes keeps the
/// list at 32 bytes instead of 40. A <see cref="BlockNode"/> derives from this list, so a block
/// owns its children's fields instead of pointing at a separate 32-byte list object (a block node
/// is 64 bytes instead of 56 + 32). Equality stays by reference: block trees are compared and
/// hashed by identity, never structurally.
/// </summary>
internal record BlockChildList : BlockChild, IReadOnlyList<BlockChild>
{
    // null (no children), the lone BlockChild, or a BlockChild[] once a second child arrived.
    private object? items;
    private int count;

    // Identity equality: the synthesized record members would compare and hash the children,
    // walking a whole subtree (and two distinct blocks could compare equal).
    public virtual bool Equals(BlockChildList? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    public int Count => count;

    /// <summary>1 for the lone-child field, otherwise the length of the children array.</summary>
    public int Capacity => items is BlockChild[] array ? array.Length : 1;

    public BlockChild this[int index]
        => (uint)index < (uint)count
            ? (items is BlockChild[] array ? array[index] : (BlockChild)items!)
            : throw new ArgumentOutOfRangeException(nameof(index));

    internal void Add(BlockChild child)
    {
        if (items is BlockChild[] array)
        {
            if (count == array.Length)
            {
                Array.Resize(ref array, checked(array.Length * 2));
                items = array;
            }
            array[count] = child;
        }
        else if (count == 0) items = child;
        else items = new BlockChild[4] { (BlockChild)items!, child, null!, null! };
        count++;
    }

    /// <summary>
    /// Moves <paramref name="source"/>'s tail from <paramref name="start"/> into this empty list:
    /// one child into the lone-child slot, more into one array of exactly their count.
    /// </summary>
    internal void TakeChildren(List<BlockChild> source, int start)
    {
        if (count != 0) throw new InvalidOperationException("Children are taken only into an empty list.");
        var taken = source.Count - start;
        if (taken == 1) items = source[start];
        else if (taken > 1)
        {
            var array = new BlockChild[taken];
            source.CopyTo(start, array, 0, taken);
            items = array;
        }
        count = taken;
        source.RemoveRange(start, taken);
    }

    internal void RemoveAt(int index)
    {
        if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
        if (items is BlockChild[] array)
        {
            // Shift the later children down over the removed one and clear the vacated slot.
            for (var i = index; i < count - 1; i++) array[i] = array[i + 1];
            array[count - 1] = null!;
        }
        else items = null;
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

// A replaced element: a bound raster image, or an inline svg read into vector shapes (Vector).
internal sealed record ImageNode(PdfImage? Image, TextStyle Style, BoxStyle BoxStyle,
    int SourceOffset, string? Alt, SvgGraphic? Vector = null) : BlockChild
{
    // The intrinsic aspect ratio's terms: the pixel size, or the svg's viewBox size.
    internal double RatioWidth => Vector?.RatioWidth ?? Image!.PixelWidth;
    internal double RatioHeight => Vector?.RatioHeight ?? Image!.PixelHeight;
    // The natural width in points (one CSS px is 0.75pt); an svg's default is 300px.
    internal double NaturalWidth => Vector is null ? Image!.PixelWidth * 0.75 : 225;
    internal int CommandCount => Vector?.CommandCount ?? 1;
    // object-fit and object-position of a raster image; null for fill, which stretches it to the box.
    internal ObjectPlacement? Placement => BoxStyle.ObjectPlacement.Fit == ObjectFit.Fill || Image is null ? null : BoxStyle.ObjectPlacement;
}

internal sealed class DocumentRoot
{
    public BlockChildList Children { get; } = new();
    public bool HasLists { get; set; }
    /// <summary>The compiled <c>@page</c> rules, or null when the stylesheets have none.</summary>
    public CssPageRules? PageRules { get; set; }
    /// <summary>Whether a media query read the page area (width, height, aspect-ratio or orientation).</summary>
    public bool UsesPageAreaMedia { get; set; }
    /// <summary>The html element's computed text style, which the page context inherits; null without an html element.</summary>
    public TextStyle? RootStyle { get; set; }
    /// <summary>The html element's background-color, which a browser prints over each whole sheet; null without one.</summary>
    public Rgb? CanvasColor { get; set; }
    // Source offset of an html element computed as display:flex, or -1. Layout accepts it only
    // where its single body item fills the page width, which makes it a block layout no-op.
    public int FlexRootOffset { get; set; } = -1;
    // The body of an html flex container whose specified width exceeds the page content width:
    // as a flex item (flex-shrink 1) it shrinks to that width, so layout ignores its width.
    public BlockNode? ShrunkBody { get; set; }
}
