using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace FactsPDF;

internal static class TextLines
{
    // An inline img or svg is one atomic glyph: its Scalar is -(run index + 2) in its paragraph
    // (-1 stays "no glyph" for the break scans) and Width1000 holds its outer width in points;
    // letter- and word-spacing never apply to it.
    // An inline box's start or end (see InlineBox) is a glyph too: its Scalar is -(run index + 2)
    // like an atom's and Width1000 holds the width of its edges in points, but line breaking sees
    // through it (the break analysis runs over the text and atoms alone).
    internal enum GlyphKind : byte { Text, Atom, BoxStart, BoxEnd }
    internal readonly record struct Glyph(int Scalar, TextStyle Style, PdfFont? Font, double Width1000, int SourceOffset = -1,
        GlyphKind Kind = GlyphKind.Text)
    {
        internal bool IsAtom => Kind == GlyphKind.Atom;
        internal bool IsBoxEdge => Kind >= GlyphKind.BoxStart;
        internal bool IsFixedWidth => Kind != GlyphKind.Text;
        internal int AtomRun => -Scalar - 2;
        // PDF Tc adds fixed character spacing after each shown glyph, across
        // ASCII and embedded CID TrueType font runs. Layout must count the
        // identical advance before wrapping, centring or underlining.
        public double Width => IsFixedWidth ? Width1000 : Style.FontSize * Width1000 / 1000 + Style.LetterSpacing
            + (Scalar == 32 ? Style.WordSpacing : 0);
    }
    // An inline image placed on a line: its geometry and the height of its outer box's top above
    // the line's baseline, in the line's glyph order.
    internal readonly record struct InlineAtom(ImageNode Node, ImageLayout.Geometry Geometry, double Top, string? LinkUri);
    // Glyph and atom storage is borrowed until the next MoveNext. Each consumer copies
    // every placed text run before advancing; the iterator reuses its glyph storage.
    // One line's piece of an inline box: its baseline Shift above the line's baseline (negative lowers
    // it), the font box (Ascent and Descent in points) its background and borders surround, the edges
    // that lie above and below that box and whether it continues from an earlier line. The remaining
    // fields are the wrapper's working state while it places the line.
    internal struct InlineFragment
    {
        internal InlineBox Box;
        internal double Shift, Ascent, Descent, MarginLeft, MarginRight, PaddingTop, PaddingBottom;
        internal bool Continued;
        internal double Relative, SubAscent, SubDescent;
        internal int Parent, Root;
        internal bool Deferred;
    }
    internal readonly record struct Line(List<Glyph> Glyphs, double Width, double Height, double Ascent, double Descent,
        double FirstLineIndent = 0, TextAlignment? AlignmentOverride = null, List<InlineAtom>? Atoms = null,
        List<InlineFragment>? Fragments = null);

    /// <summary>
    /// The used box of an inline img or svg in a line whose containing block is
    /// <paramref name="containingWidth"/> wide; NaN measures it for automatic table layout, where a
    /// percentage size has nothing to resolve against.
    /// </summary>
    internal static ImageLayout.Geometry AtomGeometry(ImageNode image, double containingWidth)
    {
        if (!double.IsNaN(containingWidth)) return ImageLayout.Resolve(image, containingWidth);
        var style = image.BoxStyle;
        if (style.Width is { IsPercent: true } || style.MinWidth is { IsPercent: true } || style.MaxWidth is { IsPercent: true }
            || style.Height is { IsPercent: true } || style.MinHeight is { IsPercent: true } || style.MaxHeight is { IsPercent: true }
            || style.PaddingLeft.IsPercent || style.PaddingRight.IsPercent || style.PaddingTop.IsPercent || style.PaddingBottom.IsPercent)
            throw new FactsPdfException("FPDF1302",
                "Percentage image sizes are not supported in automatic table layout; give every column an explicit col width.", image.SourceOffset);
        // Without percentages the outer width does not depend on the containing width; the largest
        // supported length only lets the fit check pass.
        return ImageLayout.Resolve(image, 14_400);
    }

    /// <summary>
    /// Appends a line's text runs and inline images. Images go to <paramref name="images"/> with
    /// their top measured downward from <paramref name="topOrigin"/> (the page height for page
    /// coordinates, 0 for a table's relative ones), as the baseline is measured upward from it.
    /// </summary>
    internal static void AppendLine(Line line, double x, double baseline, List<PlacedText> target,
        Action<int, int> charge, CancellationToken cancellation, int sourceOffset = -1,
        List<PlacedImage>? images = null, double topOrigin = 0)
    {
        var atom = 0;
        var nextFragment = 0;
        // The boxes open at this point, innermost last: the fragment, where its box began and where its
        // paint goes among the line's images (a box paints under everything inside it).
        var open = line.Fragments is null ? null : OpenFragments ??= [];
        open?.Clear();
        if (line.Fragments is { } continued)
            while (nextFragment < continued.Count && continued[nextFragment].Continued)
                open!.Add((nextFragment++, x, images?.Count ?? 0));
        var shift = open is { Count: > 0 } ? line.Fragments![open[^1].Fragment].Shift : 0;
        void Close(bool continues, double right)
        {
            var (index, left, imageIndex) = open![^1];
            open.RemoveAt(open.Count - 1);
            shift = open.Count > 0 ? line.Fragments![open[^1].Fragment].Shift : 0;
            var fragment = line.Fragments![index];
            var box = fragment.Box;
            if (box.Style.Visibility != TextVisibility.Visible) return;
            var omitLeft = fragment.Continued;
            var boxLeft = omitLeft ? left : left + fragment.MarginLeft;
            var boxRight = continues ? right : right - fragment.MarginRight;
            var baselineY = baseline + fragment.Shift;
            var top = baselineY + fragment.Ascent + fragment.PaddingTop + box.Box.BorderTop.EffectiveWidth;
            var height = fragment.Ascent + fragment.Descent + fragment.PaddingTop + fragment.PaddingBottom
                + box.Box.BorderTop.EffectiveWidth + box.Box.BorderBottom.EffectiveWidth;
            var width = Math.Max(0, boxRight - boxLeft);
            var painted = new PaintedBox(0, 0, width, height, box.Box, box.Style.Color, 0, SourceOffset: box.SourceOffset,
                OmitLeftEdge: omitLeft, OmitRightEdge: continues);
            var commands = painted.CommandCount;
            if (commands == 0) return;
            charge(commands, box.SourceOffset);
            if (images is null)
                throw new FactsPdfException("FPDF1201", "A background or border on an inline element is not supported here (table captions and page headers and footers).", box.SourceOffset);
            images.Insert(Math.Min(imageIndex, images.Count),
                new PlacedImage(null, boxLeft, topOrigin - top, width, height, SourceOffset: box.SourceOffset, Decoration: painted));
        }
        for (var start = 0; start < line.Glyphs.Count;)
        {
            var first = line.Glyphs[start];
            if (first.Kind == GlyphKind.BoxStart)
            {
                if (line.Fragments is not null && nextFragment < line.Fragments.Count)
                {
                    open!.Add((nextFragment, x, images?.Count ?? 0));
                    shift = line.Fragments[nextFragment++].Shift;
                }
                x += first.Width; start++;
                continue;
            }
            if (first.Kind == GlyphKind.BoxEnd)
            {
                if (open is { Count: > 0 }) Close(false, x + first.Width);
                x += first.Width; start++;
                continue;
            }
            if (!first.IsAtom && first.Style.Visibility != TextVisibility.Visible)
            {
                // visibility:hidden keeps the text's room and paints nothing.
                var hidden = 0d;
                var stop = start;
                while (stop < line.Glyphs.Count && line.Glyphs[stop].Kind == GlyphKind.Text && line.Glyphs[stop].Style.Visibility != TextVisibility.Visible)
                    hidden += line.Glyphs[stop++].Width;
                x += hidden; start = stop;
                continue;
            }
            if (first.IsAtom)
            {
                var placed = line.Atoms![atom++];
                var node = placed.Node;
                if (node.Style.Visibility != TextVisibility.Visible) { x += first.Width; start++; continue; }
                if (images is null) throw new InvalidOperationException("A line with inline images needs an image target.");
                var geometry = placed.Geometry;
                var outerTop = topOrigin - baseline - placed.Top;
                // The image's own padding, border and background travel with it and paint just
                // before it, above the block backgrounds (inline content is above them).
                var commands = PaintedBox.CountCommands(geometry.OuterWidth, geometry.OuterHeight, node.BoxStyle);
                charge(commands, node.SourceOffset);
                var decoration = commands == 0 ? null : new PaintedBox(-geometry.Left, -geometry.Top,
                    geometry.OuterWidth, geometry.OuterHeight, node.BoxStyle, node.Style.Color, 0, SourceOffset: node.SourceOffset);
                charge(node.CommandCount + (placed.LinkUri is null ? 0 : 1), node.SourceOffset);
                if (geometry.Width > 0 && geometry.Height > 0)
                    images.Add(new PlacedImage(node.Image, x + geometry.Left, outerTop + geometry.Top,
                        geometry.Width, geometry.Height, node.Vector, placed.LinkUri, node.SourceOffset, decoration));
                x += first.Width;
                start++;
                continue;
            }
            // Charge each real decoration path before retaining the run;
            // a combined underline/strike/overline uses up to three commands.
            charge(1 + TextDecorationCommands.Count(first.Style.Decoration)
                + (first.Style.LinkUri is null ? 0 : 1), sourceOffset); var width = 0d; var end = start; var utf16Length = 0;
            // A tab is a space glyph of its own whose advance reaches the next tab stop.
            if (first.Scalar == 9)
            {
                utf16Length = 1; width = first.Width; end = start + 1;
            }
            while (first.Scalar != 9 && end < line.Glyphs.Count && line.Glyphs[end].Kind == GlyphKind.Text && line.Glyphs[end].Style == first.Style
                && ReferenceEquals(line.Glyphs[end].Font, first.Font) && line.Glyphs[end].Scalar != 9)
            {
                if ((end & 255) == 0) cancellation.ThrowIfCancellationRequested();
                var glyph = line.Glyphs[end++];
                if (!IsInvisible(glyph.Scalar)) utf16Length = checked(utf16Length + (glyph.Scalar <= 65535 ? 1 : 2));
                width += glyph.Width;
            }
            var text = first.Scalar == 9 ? " " : string.Create(utf16Length,
                (Glyphs: line.Glyphs, Start: start, End: end, Cancellation: cancellation),
                static (destination, state) =>
                {
                    var offset = 0;
                    for (var i = state.Start; i < state.End; i++)
                    {
                        if ((i & 255) == 0) state.Cancellation.ThrowIfCancellationRequested();
                        if (!IsInvisible(state.Glyphs[i].Scalar)) offset += new Rune(state.Glyphs[i].Scalar).EncodeToUtf16(destination[offset..]);
                    }
                });
            // Only default-ignorable characters: nothing to paint, just their (zero) advance.
            if (text.Length == 0) { x += width; start = end; continue; }
            var runBaseline = baseline + shift;
            target.Add(sourceOffset >= 0 && (first.Style.Decoration != TextDecoration.None || first.Style.LinkUri is not null)
                ? new LocatedText(text, x, runBaseline, first.Style, first.Font, width, sourceOffset)
                : new PlacedText(text, x, runBaseline, first.Style, first.Font, width));
            x += width; start = end;
        }
        // Boxes still open end with the line and continue on the next one.
        while (open is { Count: > 0 }) Close(true, x);
    }

    [ThreadStatic] private static List<(int Fragment, double Left, int ImageIndex)>? OpenFragments;

    /// <summary>
    /// Ends a line that overflows its box (text-overflow:ellipsis) with an ellipsis: the glyphs that fit beside
    /// it stay. Chrome draws U+2026 where the font has it and three full stops otherwise.
    /// </summary>
    internal static Line Ellipsize(Line line, double available, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, int sourceOffset)
    {
        var glyphs = line.Glyphs;
        if (glyphs.Count == 0) return line;
        var style = glyphs[^1].Style;
        Glyph[] ellipsis;
        try { ellipsis = [Resolve(0x2026, style, sourceOffset, options, resolved)]; }
        catch (FactsPdfException failure) when (failure.Code is "FPDF1504" or "FPDF1301")
        {
            var stop = Resolve('.', style, sourceOffset, options, resolved);
            ellipsis = [stop, stop, stop];
        }
        var ellipsisWidth = ellipsis.Sum(glyph => glyph.Width);
        var width = 0d;
        foreach (var glyph in glyphs) width += glyph.Width;
        while (glyphs.Count > 0 && (width + ellipsisWidth > available + 0.000001 || glyphs[^1].Scalar == 32 && !glyphs[^1].Style.PreserveSpaces))
        {
            width -= glyphs[^1].Width;
            glyphs.RemoveAt(glyphs.Count - 1);
        }
        glyphs.AddRange(ellipsis);
        return line with { Width = width + ellipsisWidth };
    }

    internal static bool HasTextContent(Paragraph paragraph, CancellationToken cancellation)
    {
        // Match Wrap's HTML whitespace and explicit-break rules without
        // resolving glyphs or building any temporary text representation.
        foreach (var run in paragraph.Runs)
        {
            cancellation.ThrowIfCancellationRequested();
            if (run is InlineImageRun) return true;
            if (run is InlineBoxRun { IsStart: true } boxRun && boxRun.Box.HasHorizontalEdge) return true;
            if (run.IsBreak) continue;
            foreach (var character in run.Text)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!HtmlTokens.Space(character) || paragraph.Style.PreserveSpaces && (character == ' ' || character == '\t')) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The paragraph's min-content and max-content widths, measured with the glyph advances, break
    /// opportunities and first-line indent that <see cref="Wrap"/> uses, so wrapping at either width
    /// never overflows. The indent must be an absolute length.
    /// </summary>
    /// <remarks>
    /// Max-content is the widest line between forced breaks (<c>br</c> and preserved newlines).
    /// Min-content is the widest unbreakable segment between soft wrap opportunities: a word, a CJK
    /// character, a single glyph under <c>word-break:break-all</c>, <c>word-break:break-word</c> or
    /// <c>overflow-wrap:anywhere</c> (whose emergency breaks count for min-content) and a whole
    /// line under <c>white-space:nowrap</c>. Spaces that hang at the end of a line add nothing.
    /// <c>overflow-wrap:break-word</c> leaves both unchanged, as in CSS. The first-line indent is added to the first line and to its first segment.
    /// </remarks>
    internal static (double Min, double Max) IntrinsicWidths(Paragraph paragraph, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        using var lines = LineWrapper.Rent(paragraph, double.NaN, options, resolved, null, 0, cancellation).GetEnumerator();
        return lines.MeasureIntrinsic();
    }

    internal static WrappedLines Wrap(Paragraph paragraph, double available, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation,
        ListMarker? firstMarker = null, double firstLineMarkerInset = 0) =>
        LineWrapper.Rent(paragraph, available, options, resolved, firstMarker, firstLineMarkerInset, cancellation);

    // The lines of one Wrap call: a value that names the pooled wrapper and the rental it was
    // handed out for. Once that rental is disposed the wrapper may serve another Wrap, so a stale
    // handle's MoveNext returns false and its Dispose does nothing instead of touching that Wrap.
    // foreach and GetEnumerator use the struct enumerator without allocating; the interface is for
    // LINQ in tests and boxes.
    internal readonly struct WrappedLines(LineWrapper wrapper, int rental) : IEnumerable<Line>
    {
        public Enumerator GetEnumerator() => new(wrapper, rental);
        IEnumerator<Line> IEnumerable<Line>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal readonly struct Enumerator(LineWrapper wrapper, int rental) : IEnumerator<Line>
        {
            public Line Current => wrapper.CurrentFor(rental);
            object IEnumerator.Current => Current;
            public bool MoveNext() => wrapper.MoveNextFor(rental);
            internal (double Min, double Max) MeasureIntrinsic() => wrapper.MeasureFor(rental);
            public void Dispose() => wrapper.DisposeFor(rental);
            public void Reset() => throw new NotSupportedException();
        }
    }

    // Breaks one paragraph into lines, one line per MoveNext. The paragraph's glyphs are read once into a
    // pooled list and their soft wrap opportunities worked out over the whole text (see
    // LineBreakAnalyzer), then each MoveNext fills one line greedily, as Chrome does: a line ends at
    // the last opportunity before the glyph that no longer fits, a glyph that fits nowhere breaks
    // the word only when overflow-wrap or word-break allows it, and otherwise the line overflows its
    // box to the next opportunity. One wrapper per thread is reused across paragraphs (taken out of
    // its slot while in use, so a Wrap opened inside another gets its own); Dispose clears the lists
    // and returns it. Callers reach it only through WrappedLines, whose rental number keeps a stale
    // handle away from a later Wrap; every enumerator of one WrappedLines shares the wrapper.
    internal sealed class LineWrapper
    {
        private const int MaxPooledGlyphs = 4096;
        [ThreadStatic] private static LineWrapper? pooled;

        // Lookahead a break decision needs (LB15b, LB19a, LB25 and LB28a read up to two units ahead), the
        // glyphs kept before the next line for the rules that read behind, and how many are read at a time.
        private const int Lookahead = 3, Overlap = 16, ReadChunk = 256;

        // The window of the paragraph being wrapped: from a little before the next line to a little past it.
        private readonly List<Glyph> all = [];
        private readonly List<Glyph> current = [];
        private readonly LineBreakAnalyzer analyzer = new();
        private int[] scalars = [];
        private byte[] opportunities = [];
        // For the break analysis, which sees text and atoms but no inline box edges: where each of those
        // sits in the window, and for each window position the nearest text or atom before it (or -1).
        private int[] textIndex = [];
        private int[] prevText = [];
        // The inline boxes open at the start of the next line, and one line's boxes with their shifts.
        private readonly List<InlineBox> openBoxes = [];
        private readonly List<InlineFragment> fragments = [];
        private readonly List<int> boxStack = [];
        private readonly List<double> atomShifts = [];
        // Created by the first line with an inline image, so text-only wrapping allocates nothing more.
        private List<InlineAtom>? atoms;
        private bool rented;
        private int rental;
        private bool started, done;
        private Paragraph paragraph = null!;
        private double available;
        private PdfOptions options = null!;
        private Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved = null!;
        private CancellationToken cancellation;
        private ListMarker? firstMarker;
        private double firstLineMarkerInset;
        private double firstIndent;
        private double width;
        private int next;
        private bool firstLine;
        private GlyphReader reader;
        private bool readerDone;

        private Line Current { get; set; }

        internal Line CurrentFor(int handle) => handle == rental && rented ? Current : default;
        internal bool MoveNextFor(int handle) => handle == rental && rented && MoveNext();
        internal (double Min, double Max) MeasureFor(int handle) => handle == rental && rented ? Measure() : default;
        internal void DisposeFor(int handle)
        {
            if (handle == rental) Dispose();
        }

        internal static WrappedLines Rent(Paragraph paragraph, double available, PdfOptions options,
            Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved,
            ListMarker? firstMarker, double firstLineMarkerInset, CancellationToken cancellation)
        {
            var wrapper = pooled ?? new LineWrapper();
            pooled = null;
            wrapper.rented = true;
            wrapper.started = wrapper.done = wrapper.readerDone = false;
            wrapper.next = 0;
            wrapper.openBoxes.Clear();
            wrapper.paragraph = paragraph;
            wrapper.available = available;
            wrapper.options = options;
            wrapper.resolved = resolved;
            wrapper.cancellation = cancellation;
            wrapper.firstMarker = firstMarker;
            wrapper.firstLineMarkerInset = firstLineMarkerInset;
            return new WrappedLines(wrapper, wrapper.rental);
        }

        private void Dispose()
        {
            // A second Dispose must not pool the wrapper again while another Wrap uses it.
            if (!rented) return;
            rented = false;
            unchecked { rental++; }
            done = true;
            all.Clear();
            current.Clear();
            atoms?.Clear();
            fragments.Clear(); openBoxes.Clear(); boxStack.Clear(); atomShifts.Clear();
            paragraph = null!; options = null!; resolved = null!; firstMarker = null;
            Current = default; cancellation = default; reader = default;
            if (all.Capacity <= MaxPooledGlyphs && current.Capacity <= MaxPooledGlyphs) pooled = this;
        }

        private bool MoveNext()
        {
            if (done) return false;
            if (!started)
            {
                Start();
                started = true;
            }
            Trim();
            if (!Has(next))
            {
                done = true;
                if (!(firstLine && firstMarker is not null)) return false;
                current.Clear();
                width = 0;
                Current = CurrentLine();
                return true;
            }
            cancellation.ThrowIfCancellationRequested();
            var soft = BuildLine();
            Current = CurrentLine(soft);
            firstLine = false;
            return true;
        }

        // Validates the whole paragraph (so a missing glyph is reported before any width or page problem,
        // in glyph order), then reads its glyphs line by line. The first-line inset is relative to the
        // paragraph's resolved content width, not to the page or its painted border box, and belongs
        // only to the first logical line; continuation pages never re-indent.
        private void Start()
        {
            var check = new GlyphReader(paragraph, options, resolved, available, cancellation);
            while (check.MoveNext()) { }
            Open(available);
            firstLine = true;
            firstIndent = paragraph.Style.FirstLineIndent.Resolve(available) + firstLineMarkerInset;
            if (!double.IsFinite(firstIndent) || firstIndent < 0) firstIndent = 0;
        }

        private void Open(double containingWidth)
        {
            all.Clear();
            next = 0;
            readerDone = false;
            reader = new GlyphReader(paragraph, options, resolved, containingWidth, cancellation);
        }

        // Reads more glyphs into the window and works out where the window may break.
        private void Fill()
        {
            var added = 0;
            while (added < ReadChunk && !readerDone)
            {
                if (reader.MoveNext()) { all.Add(reader.Current); added++; }
                else readerDone = true;
            }
            Analyze();
        }

        private void Analyze()
        {
            var count = all.Count;
            if (scalars.Length < count)
            {
                scalars = new int[Math.Max(count, scalars.Length * 2)];
                opportunities = new byte[scalars.Length];
                textIndex = new int[scalars.Length];
                prevText = new int[scalars.Length];
            }
            // Inline box edges are invisible to the analysis: it runs over the text and atoms alone.
            var texts = 0;
            var last = -1;
            for (var i = 0; i < count; i++)
            {
                prevText[i] = last;
                if (all[i].IsBoxEdge) continue;
                scalars[texts] = all[i].Scalar;
                textIndex[texts++] = i;
                last = i;
            }
            var style = paragraph.Style;
            var found = analyzer.Analyze(scalars.AsSpan(0, texts), style.LineBreak,
                style.KeepAll ? WordBreakMode.KeepAll : style.BreakAll ? WordBreakMode.BreakAll : WordBreakMode.Normal);
            Array.Clear(opportunities, 0, count);
            for (var k = 1; k < texts; k++)
            {
                // A break between two text positions falls before the start edges of the boxes that open
                // there and after the end edges of those that close, so an edge stays with the text it
                // touches: start edges begin the next line, end edges finish the line before.
                var slot = textIndex[k];
                for (var i = textIndex[k - 1] + 1; i < textIndex[k]; i++)
                    if (all[i].Kind == GlyphKind.BoxStart) { slot = i; break; }
                var before = all[textIndex[k - 1]];
                // break-spaces: a soft wrap opportunity follows every preserved space, even before another one.
                if (before.Scalar == 32 && before.Style.BreakSpaces && all[textIndex[k]].Scalar != 10)
                    opportunities[slot] = LineBreakAnalyzer.Allowed;
                // A soft hyphen breaks only where hyphens is manual (the initial value).
                else if (before.Scalar == 0xAD && before.Style.NoHyphens) opportunities[slot] = LineBreakAnalyzer.No;
                else opportunities[slot] = found[k];
            }
        }

        // The next text or atom at or after window position i, or -1.
        private int NextText(int i)
        {
            for (; i < all.Count; i++) if (!all[i].IsBoxEdge) return i;
            return -1;
        }

        // Whether glyph `index` exists, with its break decision final.
        private bool Has(int index)
        {
            while (!readerDone && index + Lookahead >= all.Count) Fill();
            return index < all.Count;
        }

        // Drops the glyphs of finished lines, keeping a few before the next one.
        private void Trim()
        {
            var drop = next - Overlap;
            if (drop < 4 * ReadChunk) return;
            all.RemoveRange(0, drop);
            next -= drop;
            Analyze();
        }

        private static bool IsCollapsibleSpace(Glyph glyph) => glyph.Scalar == 32 && !glyph.Style.PreserveSpaces;

        // A space that hangs past the end of its line instead of forcing a wrap: a collapsible one, or a
        // preserved one that may wrap (pre-wrap, but not pre or break-spaces).
        private static bool Hangs(Glyph glyph) => glyph.Scalar == 32
            && (!glyph.Style.PreserveSpaces || !glyph.Style.NoWrap && !glyph.Style.BreakSpaces);

        // Whether a line may end before glyph i. A space's own white-space decides after it; elsewhere
        // wrapping is off only where both sides forbid it.
        private bool MayWrapBefore(int i)
        {
            if (opportunities[i] == LineBreakAnalyzer.No || prevText[i] < 0) return false;
            var previous = all[prevText[i]];
            var next = all[i].IsBoxEdge ? NextText(i) : i;
            if (next < 0) return false;
            return previous.Scalar == 32 ? !previous.Style.NoWrap : !(previous.Style.NoWrap && all[next].Style.NoWrap);
        }

        // The advance of a glyph at offset x of its line; a tab runs to the next tab stop.
        private static double Advance(Glyph glyph, double x)
        {
            if (glyph.Scalar != 9) return glyph.Width;
            var style = glyph.Style;
            var space = style.FontSize * glyph.Width1000 / 1000 + style.LetterSpacing + style.WordSpacing;
            var stop = style.TabSizeIsLength ? style.TabSize : style.TabSize * space;
            if (!(stop > 0)) return 0;
            return stop - x % stop;
        }

        // Fills `current` with the next line and returns whether it ended at a soft wrap.
        private bool BuildLine()
        {
            var start = next;
            var capacity = (firstLine ? available - firstIndent : available) + 0.000001;
            var fit = 0d;      // width up to the last glyph that cannot hang
            var total = 0d;    // width including hanging spaces
            var lastBreak = -1;
            var end = -1;
            var soft = false;
            var offset = firstLine ? firstIndent : 0;
            // An inside list marker is inline content of the first line: a first word that fits a whole line
            // but not what the marker leaves moves to the second line, leaving the marker alone on the first.
            if (firstLine && firstMarker is { Inside: true } && firstLineMarkerInset > 0 && Has(start) && all[start].Scalar != 10
                && FirstSegmentWidth(start) is var segmentWidth && segmentWidth > capacity && segmentWidth <= available + 0.000001)
            {
                current.Clear();
                width = 0;
                return true;
            }
            for (var i = start; Has(i); i++)
            {
                var glyph = all[i];
                if (glyph.Scalar == 10) { end = i; next = i + 1; break; }
                // A line that ends at a soft hyphen shows a hyphen, which has to fit as well.
                if (i > start && MayWrapBefore(i) && (all[prevText[i]].Scalar != 0xAD || fit + HyphenWidth(all[prevText[i]]) <= capacity)) lastBreak = i;
                var advance = Advance(glyph, offset + total);
                var hangs = Hangs(glyph);
                var candidate = hangs ? fit : total + advance;
                if (!glyph.Style.NoWrap && candidate > capacity)
                {
                    if (lastBreak > start) { end = lastBreak; next = lastBreak; soft = true; break; }
                    if (i > start && glyph.Kind != GlyphKind.BoxEnd && (glyph.Style.EmergencyBreak || glyph.Style.BreakAnywhere))
                    {
                        end = i; next = i; soft = true; break;
                    }
                }
                total += advance;
                if (!hangs) fit = total;
            }
            if (end < 0) { end = all.Count; next = all.Count; }
            // The line is glyphs [start, end): copy them, fixing each tab's advance to its stop, and drop the
            // collapsible spaces at its end.
            current.Clear();
            width = 0;
            for (var g = start; g < end; g++)
            {
                var glyph = all[g];
                if (glyph.Scalar == 9)
                {
                    var advance = Advance(glyph, offset + width);
                    glyph = glyph with { Width1000 = (advance - glyph.Style.LetterSpacing) * 1000 / glyph.Style.FontSize };
                }
                current.Add(glyph);
                width += glyph.Width;
            }
            // A soft hyphen that ends the line becomes a visible hyphen.
            if (soft && end > start && prevText[end] >= start && all[prevText[end]].Scalar == 0xAD)
            {
                var hyphen = HyphenGlyph(all[prevText[end]]);
                current.Insert(prevText[end] - start + 1, hyphen);
                width += hyphen.Width;
            }
            // The collapsible spaces at the end go; the box edges that follow them stay.
            for (var g = current.Count - 1; g >= 0; g--)
            {
                if (current[g].IsBoxEdge) continue;
                if (!IsCollapsibleSpace(current[g])) break;
                width -= current[g].Width;
                current.RemoveAt(g);
            }
            // Preserved spaces that hang at a soft wrap stay painted but take no part in aligning the line;
            // before a forced break or at the end of the text they count.
            if (soft)
                for (var g = current.Count - 1; g >= 0; g--)
                {
                    if (current[g].IsBoxEdge) continue;
                    if (current[g].Scalar != 32 || !Hangs(current[g])) break;
                    width -= current[g].Width;
                }
            return soft;
        }

        // The width of the unbreakable segment that starts at glyph `start`, without its hanging spaces.
        private double FirstSegmentWidth(int start)
        {
            var sum = 0d;
            for (var i = start; Has(i) && all[i].Scalar != 10; i++)
            {
                if (i > start && MayWrapBefore(i)) break;
                if (!Hangs(all[i])) sum += Advance(all[i], sum);
            }
            return sum;
        }

        // The hyphen a line that breaks at a soft hyphen ends with: U+2010 where the font has it, else a hyphen-minus.
        private Glyph HyphenGlyph(Glyph softHyphen)
        {
            try { return Resolve(0x2010, softHyphen.Style, softHyphen.SourceOffset, options, resolved); }
            catch (FactsPdfException failure) when (failure.Code is "FPDF1504" or "FPDF1301")
            {
                return Resolve('-', softHyphen.Style, softHyphen.SourceOffset, options, resolved);
            }
        }

        private double HyphenWidth(Glyph softHyphen) => HyphenGlyph(softHyphen).Width;

        // The widths IntrinsicWidths reports: the glyphs as a table cell or flex item sees them.
        internal (double Min, double Max) Measure()
        {
            Open(double.NaN);
            var indent = paragraph.Style.FirstLineIndent.Value;
            double min = 0, max = 0, line = 0, segment = 0, hanging = 0;
            var firstLineOfText = true;
            var firstSegment = true;
            var lineStarted = false;
            void EndSegment()
            {
                if (segment > 0)
                {
                    min = Math.Max(min, firstLineOfText && firstSegment ? segment + indent : segment);
                    firstSegment = false;
                }
                segment = 0; hanging = 0;
            }
            for (var i = 0; Has(i); i++)
            {
                if ((i & 1023) == 1023) { next = i; Trim(); i = next; }
                var glyph = all[i];
                if (glyph.Scalar == 10)
                {
                    EndSegment();
                    max = Math.Max(max, lineStarted && firstLineOfText ? line + indent : line);
                    line = 0; firstLineOfText = false; lineStarted = false; hanging = 0;
                    continue;
                }
                lineStarted = true;
                var advance = Advance(glyph, line);
                if (!glyph.Style.NoWrap && i > 0 && all[i - 1].Scalar != 10
                    && (glyph.Style.BreakAnywhere || glyph.Style.BreakWordsByWordBreak || MayWrapBefore(i)))
                    EndSegment();
                line += advance;
                if (Hangs(glyph)) { hanging += advance; continue; }
                segment += hanging + advance; hanging = 0;
                // A preserved space is painted and can wrap on its own, so a line must hold one.
                if (glyph.Scalar == 32) min = Math.Max(min, firstLineOfText && firstSegment ? advance + indent : advance);
            }
            EndSegment();
            max = Math.Max(max, lineStarted && firstLineOfText ? line + indent : line);
            return (min, Math.Max(min, max));
        }

        // CSS Inline Layout: a specified (non-normal) line-height uses the
        // metrics of the *first available font*, even if fallback fonts draw
        // some glyphs. Each inline style contributes its own leading-adjusted
        // ascent/descent; the parent's invisible strut is always present.
        // Using glyph fallback hhea metrics here incorrectly moves all text
        // baselines on mixed Latin/CJK lines and inflates explicit line-height.
        private (double Ascent, double Descent) Metrics(TextStyle style, int sourceOffset)
        {
            var fonts = FontRoles.Chain(options, FontRoles.Role(style), sourceOffset);
            var size = style.FontSize;
            if (style.AbsoluteLineHeight is null && style.HasNormalLineHeight && fonts.Count > 0)
                return NormalMetrics(fonts[0], size);
            var ascent = size * (fonts.Count == 0 ? 0.8 : fonts[0].Ascent1000 / 1000);
            var descent = size * (fonts.Count == 0 ? 0.2 : -fonts[0].Descent1000 / 1000);
            var leading = (LineHeight(style, fonts) - ascent - descent) / 2;
            return (ascent + leading, descent + leading);
        }

        // The height of one line of text in this style: the specified line-height, or for normal the
        // first font's ascent, descent and line gap as Chrome (Blink's SimpleFontData) adds them,
        // each rounded to whole CSS pixels. The built-in font has no metrics to read and keeps 1.2.
        private static double LineHeight(TextStyle style, IReadOnlyList<PdfFont> fonts)
        {
            if (style.AbsoluteLineHeight is { } absolute) return absolute;
            if (!style.HasNormalLineHeight) return style.FontSize * style.LineHeight;
            if (fonts.Count == 0) return style.FontSize * 1.2;
            return NormalLineHeight(fonts[0], style.FontSize);
        }

        private const double PixelsPerPoint = 4d / 3;

        // Blink's whole-pixel ascent, descent and line gap of a font at a size.
        private static (double Ascent, double Descent, double Gap) PixelMetrics(PdfFont font, double fontSize)
        {
            var pixels = fontSize * PixelsPerPoint;
            return (Math.Round(font.Ascent1000 / 1000 * pixels, MidpointRounding.AwayFromZero),
                Math.Round(-font.Descent1000 / 1000 * pixels, MidpointRounding.AwayFromZero),
                Math.Round(font.LineGap1000 / 1000 * pixels, MidpointRounding.AwayFromZero));
        }

        internal static double NormalLineHeight(PdfFont font, double fontSize)
        {
            var (ascent, descent, gap) = PixelMetrics(font, fontSize);
            return (ascent + descent + gap) / PixelsPerPoint;
        }

        // The line box's ascent and descent for line-height:normal: the line gap is leading, whose floored half
        // goes above the rounded ascent (Blink's AddLeading), the rest below.
        private static (double Ascent, double Descent) NormalMetrics(PdfFont font, double fontSize)
        {
            var (ascent, descent, gap) = PixelMetrics(font, fontSize);
            var above = ascent + Math.Floor(gap / 2);
            return (above / PixelsPerPoint, (ascent + descent + gap - above) / PixelsPerPoint);
        }

        private Line CurrentLine(bool softWrapped = false)
        {
            // Only a real automatic word wrap may justify. Explicit <br>,
            // final paragraph lines and nowrap content retain their measured
            // ragged end. Stretch existing collapsed U+0020 space advances;
            // this uses the same WordSpacing/TJ paint path as authored gaps.
            var paintedWidth = width;
            if (softWrapped && paragraph.Style.Alignment == TextAlignment.Justify ||
                !softWrapped && paragraph.Style.LastLineAlignment == TextAlignment.Justify)
            {
                var spaces = 0;
                foreach (var glyph in current)
                    if (glyph.Scalar == 32) spaces++;
                var remaining = (firstLine ? available - firstIndent : available) - width;
                if (spaces > 0 && remaining > 0.000001)
                {
                    var extra = remaining / spaces;
                    for (var i = 0; i < current.Count; i++)
                    {
                        var glyph = current[i];
                        if (glyph.Scalar == 32)
                            current[i] = glyph with
                            {
                                Style = glyph.Style with { WordSpacing = glyph.Style.WordSpacing + extra }
                            };
                    }
                    paintedWidth = width + extra * spaces;
                }
            }
            var (ascent, descent) = Metrics(paragraph.Style, paragraph.SourceOffset);
            if (firstLine && firstMarker is { } marker)
            {
                var (markerAscent, markerDescent) = Metrics(marker.Style, marker.SourceOffset);
                if (marker.Text is null)
                {
                    markerAscent = Math.Max(markerAscent, 0.45 * marker.Style.FontSize);
                    markerDescent = Math.Max(markerDescent, -0.15 * marker.Style.FontSize);
                }
                ascent = Math.Max(ascent, markerAscent);
                descent = Math.Max(descent, markerDescent);
            }
            var hasAtoms = false;
            var hasBoxes = openBoxes.Count > 0;
            foreach (var glyph in current)
            {
                hasAtoms |= glyph.IsAtom;
                hasBoxes |= glyph.IsBoxEdge;
            }
            atoms?.Clear();
            fragments.Clear();
            atomShifts.Clear();
            if (hasBoxes) (ascent, descent) = PlaceBoxes(ascent, descent);
            else
                foreach (var glyph in current)
                {
                    // An inline image's parent inline box contributes its own leading-adjusted
                    // height, as an empty inline box does in standards mode.
                    var (glyphAscent, glyphDescent) = Metrics(glyph.Style, glyph.SourceOffset);
                    ascent = Math.Max(ascent, glyphAscent);
                    descent = Math.Max(descent, glyphDescent);
                }
            if (hasAtoms) (ascent, descent) = PlaceAtoms(ascent, descent, hasBoxes);
            return new(current, paintedWidth, ascent + descent, ascent, descent,
                firstLine ? firstIndent : 0,
                softWrapped ? null : paragraph.Style.LastLineAlignment, hasAtoms ? atoms : null,
                hasBoxes ? fragments : null);
        }

        // Walks the line's glyphs with the inline boxes open at its start, giving every box its baseline
        // shift and every glyph, atom and box the line height it asks for at that shift. A box aligned to
        // the top or bottom of the line is measured on its own (with what it holds) and placed after the
        // rest, growing the line only when taller than it. Returns the line's ascent and descent.
        private (double Ascent, double Descent) PlaceBoxes(double ascent, double descent)
        {
            boxStack.Clear();
            for (var i = 0; i < openBoxes.Count; i++) PushBox(openBoxes[i], true);
            foreach (var glyph in current)
            {
                if (glyph.Kind == GlyphKind.BoxStart)
                    PushBox(((InlineBoxRun)paragraph.Runs[glyph.AtomRun]).Box, false);
                else if (glyph.Kind == GlyphKind.BoxEnd)
                {
                    if (boxStack.Count > 0) boxStack.RemoveAt(boxStack.Count - 1);
                }
                else
                {
                    if (glyph.IsAtom)
                    {
                        atomShifts.Add(boxStack.Count > 0 ? fragments[boxStack[^1]].Relative : 0);
                        // The atom's own box is added by PlaceAtoms; its parent inline box still counts.
                    }
                    var (glyphAscent, glyphDescent) = Metrics(glyph.Style, glyph.SourceOffset);
                    Contribute(glyphAscent, glyphDescent);
                }
            }
            openBoxes.Clear();
            foreach (var openIndex in boxStack) openBoxes.Add(fragments[openIndex].Box);
            var span = CollectionsMarshal.AsSpan(fragments);
            for (var i = 0; i < span.Length; i++)
            {
                ref var fragment = ref span[i];
                if (!fragment.Deferred) continue;
                var height = fragment.SubAscent + fragment.SubDescent;
                var lineHeight = ascent + descent;
                if (fragment.Box.Align.Kind == InlineAlignKind.Top)
                {
                    if (height > lineHeight) descent += height - lineHeight;
                    fragment.Shift = ascent - fragment.SubAscent;
                }
                else
                {
                    if (height > lineHeight) ascent += height - lineHeight;
                    fragment.Shift = -(descent - fragment.SubDescent);
                }
            }
            for (var i = 0; i < span.Length; i++)
            {
                ref var fragment = ref span[i];
                if (!fragment.Deferred) fragment.Shift = fragment.Root < 0 ? fragment.Relative : span[fragment.Root].Shift + fragment.Relative;
            }
            return (ascent, descent);

            void Contribute(double glyphAscent, double glyphDescent)
            {
                var top = boxStack.Count > 0 ? boxStack[^1] : -1;
                var relative = top >= 0 ? fragments[top].Relative : 0;
                var root = top >= 0 ? fragments[top].Root : -1;
                if (root >= 0)
                {
                    var rooted = CollectionsMarshal.AsSpan(fragments);
                    rooted[root].SubAscent = Math.Max(rooted[root].SubAscent, glyphAscent + relative);
                    rooted[root].SubDescent = Math.Max(rooted[root].SubDescent, glyphDescent - relative);
                }
                else
                {
                    ascent = Math.Max(ascent, glyphAscent + relative);
                    descent = Math.Max(descent, glyphDescent - relative);
                }
            }

            void PushBox(InlineBox box, bool continued)
            {
                var parent = boxStack.Count > 0 ? boxStack[^1] : -1;
                var deferred = box.Align.Kind is InlineAlignKind.Top or InlineAlignKind.Bottom;
                var edges = box.Resolve(available);
                var (fontAscent, fontDescent) = FontBoxRounded(box.Style, box.SourceOffset);
                var index = fragments.Count;
                fragments.Add(new InlineFragment
                {
                    Box = box, Ascent = fontAscent, Descent = fontDescent, MarginLeft = edges.MarginLeft,
                    MarginRight = edges.MarginRight, PaddingTop = edges.PaddingTop, PaddingBottom = edges.PaddingBottom,
                    Continued = continued, Parent = parent, Deferred = deferred,
                    Root = deferred ? index : parent >= 0 ? fragments[parent].Root : -1,
                    Relative = deferred ? 0 : (parent >= 0 ? fragments[parent].Relative : 0) + OwnShift(box),
                });
                boxStack.Add(index);
                var (boxAscent, boxDescent) = Metrics(box.Style, box.SourceOffset);
                Contribute(boxAscent, boxDescent);
            }
        }

        // The distance vertical-align raises an inline box's baseline above its parent's (negative lowers).
        private double OwnShift(InlineBox box)
        {
            var align = box.Align;
            switch (align.Kind)
            {
                case InlineAlignKind.Sub: return -SubSuperShift(box.ParentStyle, 5);
                case InlineAlignKind.Super: return SubSuperShift(box.ParentStyle, 3);
                case InlineAlignKind.Shift:
                    return align.IsPercent
                        ? align.Shift / 100 * LineHeight(box.Style, FontRoles.Chain(options, FontRoles.Role(box.Style), box.SourceOffset))
                        : align.Shift;
                case InlineAlignKind.TextTop:
                    return FontBoxRounded(box.ParentStyle, box.SourceOffset).Ascent - FontBoxRounded(box.Style, box.SourceOffset).Ascent;
                case InlineAlignKind.TextBottom:
                    return FontBoxRounded(box.Style, box.SourceOffset).Descent - FontBoxRounded(box.ParentStyle, box.SourceOffset).Descent;
                case InlineAlignKind.Middle:
                {
                    var (ascent, descent) = Metrics(box.Style, box.SourceOffset);
                    return XHeight(box.ParentStyle, box.SourceOffset) / 2 - (ascent - descent) / 2;
                }
                default: return 0;
            }
        }

        // Blink raises super by a third and lowers sub by a fifth of the parent's font size in CSS pixels
        // (held in 1/64 pixel units, as its LayoutUnit does), plus one pixel.
        private static double SubSuperShift(TextStyle parent, int divisor)
        {
            var fixedSize = Math.Round(parent.FontSize * PixelsPerPoint * 64, MidpointRounding.AwayFromZero);
            return (Math.Floor(fixedSize / divisor) + 64) / 64 / PixelsPerPoint;
        }

        // The font box an inline box's background and borders surround: the first font's ascent and
        // descent, each rounded to whole CSS pixels as Blink does, in points.
        private (double Ascent, double Descent) FontBoxRounded(TextStyle style, int sourceOffset)
        {
            var fonts = FontRoles.Chain(options, FontRoles.Role(style), sourceOffset);
            if (fonts.Count == 0) return (style.FontSize * 0.8, style.FontSize * 0.2);
            var (ascent, descent, _) = PixelMetrics(fonts[0], style.FontSize);
            return (ascent / PixelsPerPoint, descent / PixelsPerPoint);
        }

        // CSS 2.1 §10.8: each inline image's outer box is aligned to its parent's baseline,
        // x-height or text box, which extends the line's ascent and descent; top and bottom
        // images are then aligned to the line box itself, growing it only when taller than it.
        private (double Ascent, double Descent) PlaceAtoms(double ascent, double descent, bool hasBoxes)
        {
            var atoms = this.atoms ??= [];
            var deferred = false;
            var atomIndex = 0;
            foreach (var glyph in current)
            {
                if (!glyph.IsAtom) continue;
                var raise = hasBoxes ? atomShifts[atomIndex++] : 0;
                var image = ((InlineImageRun)paragraph.Runs[glyph.AtomRun]).Image;
                var geometry = AtomGeometry(image, available);
                var height = geometry.OuterHeight;
                var align = image.BoxStyle.InlineAlign;
                var top = align.Kind switch
                {
                    InlineAlignKind.Shift => height + (align.IsPercent
                        ? align.Shift / 100 * LineHeight(glyph.Style, FontRoles.Chain(options, FontRoles.Role(glyph.Style), glyph.SourceOffset))
                        : align.Shift),
                    InlineAlignKind.Middle => (height + XHeight(glyph.Style, glyph.SourceOffset)) / 2,
                    InlineAlignKind.TextTop => FontBox(glyph.Style, glyph.SourceOffset).Ascent,
                    InlineAlignKind.TextBottom => height - FontBox(glyph.Style, glyph.SourceOffset).Descent,
                    _ => height,
                };
                if (align.Kind is InlineAlignKind.Top or InlineAlignKind.Bottom) deferred = true;
                else
                {
                    top += raise;
                    ascent = Math.Max(ascent, top);
                    descent = Math.Max(descent, height - top);
                }
                atoms.Add(new(image, geometry, top, glyph.Style.LinkUri));
            }
            if (!deferred) return (ascent, descent);
            for (var i = 0; i < atoms.Count; i++)
            {
                var kind = atoms[i].Node.BoxStyle.InlineAlign.Kind;
                var height = atoms[i].Geometry.OuterHeight;
                if (kind == InlineAlignKind.Top && height > ascent + descent) descent = height - ascent;
                else if (kind == InlineAlignKind.Bottom && height > ascent + descent) ascent = height - descent;
            }
            for (var i = 0; i < atoms.Count; i++)
            {
                var kind = atoms[i].Node.BoxStyle.InlineAlign.Kind;
                if (kind == InlineAlignKind.Top) atoms[i] = atoms[i] with { Top = ascent };
                else if (kind == InlineAlignKind.Bottom) atoms[i] = atoms[i] with { Top = atoms[i].Geometry.OuterHeight - descent };
            }
            return (ascent, descent);
        }

        // The first available font's ascent and descent at the style's size, without leading: the
        // text box text-top and text-bottom align to.
        private (double Ascent, double Descent) FontBox(TextStyle style, int sourceOffset)
        {
            var fonts = FontRoles.Chain(options, FontRoles.Role(style), sourceOffset);
            var size = style.FontSize;
            return fonts.Count == 0 ? (size * 0.8, size * 0.2)
                : (size * fonts[0].Ascent1000 / 1000, size * -fonts[0].Descent1000 / 1000);
        }

        // The first available font's x-height (Courier's 426/1000 for the built-in font).
        private double XHeight(TextStyle style, int sourceOffset)
        {
            var fonts = FontRoles.Chain(options, FontRoles.Role(style), sourceOffset);
            return style.FontSize * (fonts.Count == 0 ? 0.426 : fonts[0].XHeight1000 / 1000);
        }
    }

    private static Glyph Resolve(int scalar, TextStyle style, int sourceOffset, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved)
    {
        var role = FontRoles.Role(style);
        if (!resolved.TryGetValue((role, scalar), out var value))
        {
            var fonts = FontRoles.Chain(options, role, sourceOffset);
            if (IsInvisible(scalar))
                // Default-ignorable: no advance and nothing painted, but the line breaking rules still see it.
                value = (fonts.Count == 0 ? null : fonts[0], 0);
            else if (fonts.Count == 0)
            {
                if (scalar is < 32 or > 126) throw new FactsPdfException("FPDF1301", $"U+{scalar:X4} requires an explicitly supplied font.", sourceOffset);
                value = (null, 600);
            }
            else
            {
                CheckSimpleScalar(scalar, sourceOffset);
                PdfFont? chosen = null; ushort glyph = 0;
                foreach (var font in fonts) { glyph = font.GlyphFor(scalar); if (glyph != 0) { chosen = font; break; } }
                if (chosen is null) throw role == FontRole.Normal
                    ? new FactsPdfException("FPDF1504", $"No supplied font contains U+{scalar:X4}.", sourceOffset)
                    : new FactsPdfException("FPDF1504", $"No supplied {FontRoles.Name(role)} font contains U+{scalar:X4}.", sourceOffset);
                value = (chosen, chosen.Width1000(glyph));
            }
            resolved.Add((role, scalar), value);
        }
        return new(scalar, style, value.Font, value.Width, sourceOffset);
    }

    // Reads a paragraph's glyphs after HTML whitespace collapsing, line-break handling and
    // text-transform. A mutable value: the line wrapper keeps it as a field, so a pass costs no object.
    // containingWidth sizes inline images (NaN: automatic table layout, see AtomGeometry).
    private struct GlyphReader(Paragraph paragraph, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, double containingWidth,
        CancellationToken cancellation)
    {
        private int runIndex;
        private int textIndex;
        private bool insideRun;
        private Glyph? pendingSpace;
        // Wrap naturally suppresses a nonexistent empty final line;
        // every real segment break must still be emitted, including the
        // last of consecutive trailing newlines.
        private bool previousCarriageReturn;
        private bool afterText;
        // An ASCII word can cross differently styled inline spans, even
        // when one span explicitly resets text-transform to none.
        private bool insideAsciiWord;
        // A glyph read right after a pending collapsed space; it is resolved on the next
        // MoveNext so font diagnostics keep their order relative to the space.
        private int queuedScalar = -1;
        private TextRun? queuedRun;
        // An inline image read right after a pending collapsed space.
        private Glyph? queuedAtom;

        internal Glyph Current { get; private set; }

        internal bool MoveNext()
        {
            if (queuedAtom is { } atom)
            {
                Current = atom;
                afterText = true;
                queuedAtom = null;
                return true;
            }
            if (queuedScalar >= 0)
            {
                Current = Resolve(queuedScalar, queuedRun!.Style, queuedRun.SourceOffset, options, resolved);
                afterText = true;
                queuedScalar = -1; queuedRun = null;
                return true;
            }
            var runs = paragraph.Runs;
            while (runIndex < runs.Count)
            {
                var run = runs[runIndex];
                if (!insideRun)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (run.IsBreak)
                    {
                        // HTML <br> starts a line even after an authored newline.
                        previousCarriageReturn = false;
                        pendingSpace = null;
                        afterText = false;
                        insideAsciiWord = false;
                        runIndex++;
                        Current = new Glyph(10, run.Style, null, 0, run.SourceOffset);
                        return true;
                    }
                    if (run is InlineBoxRun boxRun)
                    {
                        // An inline box edge takes room but is no text: a collapsed space before it stays
                        // before it, and it neither ends a word nor starts the text after it.
                        var edges = boxRun.Box.Resolve(containingWidth);
                        var edge = new Glyph(-(runIndex + 2), run.Style, null, boxRun.IsStart ? edges.Start : edges.End,
                            run.SourceOffset, boxRun.IsStart ? GlyphKind.BoxStart : GlyphKind.BoxEnd);
                        runIndex++;
                        if (pendingSpace.HasValue)
                        {
                            Current = pendingSpace.Value;
                            pendingSpace = null;
                            queuedAtom = edge;
                            return true;
                        }
                        Current = edge;
                        return true;
                    }
                    if (run is InlineImageRun image)
                    {
                        // An atomic inline: a collapsed space before it is kept, one after it
                        // collapses as after text, and it ends any ASCII word.
                        previousCarriageReturn = false;
                        insideAsciiWord = false;
                        var glyph = new Glyph(-(runIndex + 2), run.Style, null,
                            AtomGeometry(image.Image, containingWidth).OuterWidth, run.SourceOffset, GlyphKind.Atom);
                        runIndex++;
                        if (pendingSpace.HasValue)
                        {
                            Current = pendingSpace.Value;
                            pendingSpace = null;
                            queuedAtom = glyph;
                            return true;
                        }
                        Current = glyph;
                        afterText = true;
                        return true;
                    }
                    insideRun = true; textIndex = 0;
                }
                while (textIndex < run.Text.Length)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (Rune.DecodeFromUtf16(run.Text.AsSpan(textIndex), out var rune, out var consumed) != OperationStatus.Done)
                        throw new FactsPdfException("FPDF1304", "Invalid UTF-16 text; unpaired surrogate.", run.SourceOffset);
                    textIndex += consumed;
                    var scalar = rune.Value;
                    if (run.Style.PreserveLineBreaks)
                    {
                        // HTML's CRLF segment break is one break even across
                        // differently styled spans; CR and FF also separate lines.
                        if (scalar == 10 && previousCarriageReturn)
                        {
                            previousCarriageReturn = false;
                            continue;
                        }
                        previousCarriageReturn = scalar == 13;
                        if (scalar is 10 or 13 or 12)
                        {
                            pendingSpace = null;
                            afterText = false;
                            insideAsciiWord = false;
                            Current = new Glyph(10, run.Style, null, 0, run.SourceOffset);
                            return true;
                        }
                    }
                    // Preserving modes retain spaces and tabs; any other control character has no glyph here, so
                    // report a stable error before any PDF bytes replace the destination.
                    if (run.Style.PreserveSpaces && (scalar is < 32 and not 9 || scalar == 127))
                        throw new FactsPdfException("FPDF1305",
                            "Preformatted report text does not support control characters other than tab and newline.",
                            run.SourceOffset);
                    // Scoped word definition: ASCII letters/digits form tokens;
                    // punctuation, spaces and non-ASCII scalars delimit them.
                    var wordCharacter = scalar is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
                    if (run.Style.Transform != TextTransform.None)
                    {
                        // ASCII-only case conversion is deterministic and cannot
                        // expand a character or change the font's glyph count.
                        // Do not silently misrender cased Unicode letters:
                        // full casing/locale rules and shaping are out of scope.
                        if (scalar > 127 &&
                            (Rune.GetUnicodeCategory(rune) is
                                (UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter)
                             // Unicode also maps some LetterNumber and OtherSymbol
                             // scalars, e.g. Roman numerals and circled letters.
                             // ASCII-only output must never leave those silently
                             // unchanged when case conversion is requested.
                             || Rune.ToUpperInvariant(rune) != rune
                             || Rune.ToLowerInvariant(rune) != rune))
                            throw new FactsPdfException("FPDF1305",
                                "Non-ASCII cased text requires Unicode case conversion beyond the supported ASCII report subset.",
                                run.SourceOffset);
                        if (run.Style.Transform == TextTransform.Uppercase && scalar is >= 'a' and <= 'z')
                            scalar -= 'a' - 'A';
                        else if (run.Style.Transform == TextTransform.Lowercase && scalar is >= 'A' and <= 'Z')
                            scalar += 'a' - 'A';
                        else if (run.Style.Transform == TextTransform.Capitalize && !insideAsciiWord && scalar is >= 'a' and <= 'z')
                            scalar -= 'a' - 'A';
                    }
                    insideAsciiWord = wordCharacter;
                    if (scalar <= 127 && HtmlTokens.Space((char)scalar))
                    {
                        if (run.Style.PreserveSpaces)
                        {
                            // Preserved spaces and tabs each keep a glyph and the authored style, even
                            // before or after a line break. A tab is a space glyph whose advance the
                            // line wrapper stretches to the next tab stop.
                            Current = Resolve(32, run.Style, run.SourceOffset, options, resolved);
                            if (scalar != 32) Current = Current with { Scalar = scalar };
                            afterText = true;
                            return true;
                        }
                        // Resolve at the original point even when a later
                        // break/end removes this space. Its font diagnostic
                        // and the first whitespace run's style are preserved.
                        if (afterText && !pendingSpace.HasValue)
                            pendingSpace = Resolve(32, run.Style, run.SourceOffset, options, resolved);
                    }
                    else
                    {
                        if (pendingSpace.HasValue)
                        {
                            Current = pendingSpace.Value;
                            pendingSpace = null;
                            queuedScalar = scalar; queuedRun = run;
                            return true;
                        }
                        Current = Resolve(scalar, run.Style, run.SourceOffset, options, resolved);
                        afterText = true;
                        return true;
                    }
                }
                insideRun = false;
                runIndex++;
            }
            return false;
        }
    }

    // Zero width space, the joiners, the word joiner, the byte order mark and the variation selectors: nothing is
    // painted for them (a font need not have a glyph) and they advance nothing, as in Chrome.
    internal static bool IsInvisible(int scalar) => scalar is 0x00AD or 0x200B or 0x200C or 0x200D or 0x2060 or 0xFEFF
        or >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF;

    private static void CheckSimpleScalar(int scalar, int sourceOffset)
    {
        var category = Rune.GetUnicodeCategory(new Rune(scalar));
        var simpleRange = scalar is >= 32 and <= 0x052f or >= 0x1e00 and <= 0x1fff or >= 0x2000 and <= 0x2bff
            or >= 0x2e80 and <= 0x2fdf or >= 0x2ff0 and <= 0x2fff
            or >= 0x3000 and <= 0x33ff or >= 0x3400 and <= 0x9fff or >= 0xa000 and <= 0xa4cf or >= 0xac00 and <= 0xd7a3
            or >= 0xf900 and <= 0xfaff or >= 0xfe30 and <= 0xfe6f or >= 0xff00 and <= 0xffef
            or >= 0x1b000 and <= 0x1b2ff or >= 0x1f200 and <= 0x1f2ff or >= 0x20000 and <= 0x3347f;
        if (!simpleRange || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark or UnicodeCategory.Format or UnicodeCategory.Control
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.OtherNotAssigned)
            throw new FactsPdfException("FPDF1305", $"U+{scalar:X4} needs text processing outside this simple horizontal Unicode subset (shaping/bidi/sequence support is not implemented).", sourceOffset);
    }
}
