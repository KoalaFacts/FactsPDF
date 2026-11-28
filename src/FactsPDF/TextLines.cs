using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Text;

namespace FactsPDF;

internal static class TextLines
{
    // U+00AD stays in a paragraph's glyphs as an invisible zero-width break opportunity: a line that
    // breaks there replaces it with a hyphen, every other line drops it (hyphens:manual); with
    // hyphens:none the reader never emits it.
    internal const int SoftHyphen = 0xAD;

    // An inline img or svg is one atomic glyph: its Scalar is -(run index + 2) in its paragraph
    // (-1 stays "no glyph" for the break scans) and Width1000 holds its outer width in points;
    // letter- and word-spacing never apply to it.
    internal readonly record struct Glyph(int Scalar, TextStyle Style, PdfFont? Font, double Width1000, int SourceOffset = -1)
    {
        internal bool IsAtom => Scalar < -1;
        internal int AtomRun => -Scalar - 2;
        // PDF Tc adds fixed character spacing after each shown glyph, across
        // ASCII and embedded CID TrueType font runs. Layout must count the
        // identical advance before wrapping, centring or underlining.
        public double Width => IsAtom ? Width1000 : Scalar == SoftHyphen ? 0 : Style.FontSize * Width1000 / 1000 + Style.LetterSpacing
            + (Scalar == 32 ? Style.WordSpacing : 0);
        internal bool IsSoftHyphen => Scalar == SoftHyphen;
    }
    // An inline image placed on a line: its geometry and the height of its outer box's top above
    // the line's baseline, in the line's glyph order.
    internal readonly record struct InlineAtom(ImageNode Node, ImageLayout.Geometry Geometry, double Top, string? LinkUri);
    // Glyph and atom storage is borrowed until the next MoveNext. Each consumer copies
    // every placed text run before advancing; the iterator reuses its glyph storage.
    internal readonly record struct Line(List<Glyph> Glyphs, double Width, double Height, double Ascent, double Descent,
        double FirstLineIndent = 0, TextAlignment? AlignmentOverride = null, List<InlineAtom>? Atoms = null);

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
        for (var start = 0; start < line.Glyphs.Count;)
        {
            var first = line.Glyphs[start];
            if (first.IsAtom)
            {
                var placed = line.Atoms![atom++];
                var node = placed.Node;
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
                images.Add(new PlacedImage(node.Image, x + geometry.Left, outerTop + geometry.Top,
                    geometry.Width, geometry.Height, node.Vector, placed.LinkUri, node.SourceOffset, decoration,
                    node.Placement));
                x += first.Width;
                start++;
                continue;
            }
            // Charge each real decoration path before retaining the run;
            // a combined underline/strike/overline uses up to three commands.
            charge(1 + TextDecorationCommands.Count(first.Style.Decoration)
                + (first.Style.LinkUri is null ? 0 : 1), sourceOffset); var width = 0d; var end = start; var utf16Length = 0;
            while (end < line.Glyphs.Count && !line.Glyphs[end].IsAtom && line.Glyphs[end].Style == first.Style
                && ReferenceEquals(line.Glyphs[end].Font, first.Font))
            {
                if ((end & 255) == 0) cancellation.ThrowIfCancellationRequested();
                var glyph = line.Glyphs[end++];
                utf16Length = checked(utf16Length + (glyph.Scalar <= 65535 ? 1 : 2));
                width += glyph.Width;
            }
            var text = string.Create(utf16Length,
                (Glyphs: line.Glyphs, Start: start, End: end, Cancellation: cancellation),
                static (destination, state) =>
                {
                    var offset = 0;
                    for (var i = state.Start; i < state.End; i++)
                    {
                        if ((i & 255) == 0) state.Cancellation.ThrowIfCancellationRequested();
                        offset += new Rune(state.Glyphs[i].Scalar).EncodeToUtf16(destination[offset..]);
                    }
                });
            target.Add(sourceOffset >= 0 && (first.Style.Decoration != TextDecoration.None || first.Style.LinkUri is not null)
                ? new LocatedText(text, x, baseline, first.Style, first.Font, width, sourceOffset)
                : new PlacedText(text, x, baseline, first.Style, first.Font, width));
            x += width; start = end;
        }
    }

    internal static bool HasTextContent(Paragraph paragraph, CancellationToken cancellation)
    {
        // Match Wrap's HTML whitespace and explicit-break rules without
        // resolving glyphs or building any temporary text representation.
        foreach (var run in paragraph.Runs)
        {
            cancellation.ThrowIfCancellationRequested();
            if (run is InlineImageRun) return true;
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
    /// Min-content is the widest unbreakable segment: a word between spaces and CJK break
    /// opportunities, a single glyph under <c>word-break:break-all</c>, <c>word-break:break-word</c> or
    /// <c>overflow-wrap:anywhere</c> (whose emergency breaks count for min-content), a whole
    /// line under <c>white-space:nowrap</c>, and also a single preserved space.
    /// <c>overflow-wrap:break-word</c> leaves both unchanged, as in CSS. The first-line indent is added to the first line and to its first segment.
    /// </remarks>
    internal static (double Min, double Max) IntrinsicWidths(Paragraph paragraph, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved, CancellationToken cancellation)
    {
        var style = paragraph.Style;
        var indent = style.FirstLineIndent.Value;
        var reader = new GlyphReader(paragraph, options, resolved, double.NaN, cancellation);
        double min = 0, max = 0, line = 0, segment = 0;
        var firstLine = true;
        var firstSegment = true;
        var lineStarted = false;
        var previous = -1;
        void EndSegment()
        {
            if (segment == 0) return;
            min = Math.Max(min, firstLine && firstSegment ? segment + indent : segment);
            firstSegment = false;
            segment = 0;
        }
        while (reader.MoveNext())
        {
            var glyph = reader.Current;
            if (glyph.Scalar == 10)
            {
                EndSegment();
                max = Math.Max(max, lineStarted && firstLine ? line + indent : line);
                line = 0; firstLine = false; lineStarted = false; previous = -1;
                continue;
            }
            lineStarted = true;
            if (style.NoWrap)
            {
                line += glyph.Width;
                segment = line;
                continue;
            }
            if (glyph.IsSoftHyphen)
            {
                // The text before a soft hyphen and its hyphen is a segment of its own.
                if (!(style.BreakAll || style.BreakWordsByWordBreak || style.BreakAnywhere))
                {
                    segment += HyphenFor(glyph, options, resolved).Width;
                    EndSegment();
                    previous = -1;
                }
                continue;
            }
            if (glyph.Scalar == 32)
            {
                EndSegment();
                line += glyph.Width;
                // A preserved space is painted and can wrap on its own, so a line must hold one.
                if (style.PreserveSpaces) min = Math.Max(min, firstLine && firstSegment ? glyph.Width + indent : glyph.Width);
                previous = 32;
                continue;
            }
            if (style.BreakAll || style.BreakWordsByWordBreak || style.BreakAnywhere || previous is not (-1 or 32) && CanBreak(previous, glyph.Scalar))
                EndSegment();
            segment += glyph.Width;
            line += glyph.Width;
            previous = glyph.Scalar;
        }
        EndSegment();
        max = Math.Max(max, lineStarted && firstLine ? line + indent : line);
        return (min, Math.Max(min, max));
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
            public void Dispose() => wrapper.DisposeFor(rental);
            public void Reset() => throw new NotSupportedException();
        }
    }

    // Breaks one paragraph into lines, one line per MoveNext. A hand-written resumable
    // state machine instead of a compiler iterator: one wrapper per thread is reused across
    // paragraphs (taken out of its slot while in use, so a Wrap opened inside another gets
    // its own), and its line and word glyph lists are reused with it. Nothing runs until the
    // first MoveNext; Dispose clears the lists and returns the wrapper. Callers reach it only
    // through WrappedLines, whose rental number keeps a stale handle away from a later Wrap;
    // every enumerator of one WrappedLines shares the wrapper, so it is enumerated once.
    internal sealed class LineWrapper
    {
        private const int MaxPooledGlyphs = 4096;
        [ThreadStatic] private static LineWrapper? pooled;

        private enum State { Start, Loop, AfterBreak, AfterPreservedSpaceWrap, AfterBreakWordStart, AfterBreakWordGlyph, AfterWordWrap, AfterSoftHyphenBreak, Done }

        private readonly List<Glyph> current = [];
        private readonly List<Glyph> word = [];
        // Created by the first line with an inline image, so text-only wrapping allocates nothing more.
        private List<InlineAtom>? atoms;
        private bool rented;
        private int rental;
        private State state;
        private Paragraph paragraph = null!;
        private double available;
        private PdfOptions options = null!;
        private Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved = null!;
        private CancellationToken cancellation;
        private ListMarker? firstMarker;
        private double firstLineMarkerInset;
        private double firstIndent;
        private GlyphReader cursor;
        private bool hasGlyph;
        private double width;
        private Glyph? space;
        private bool firstLine;
        private double wordWidth;
        private double gap;
        private int wordIndex;
        // The word collected so far holds a soft hyphen; resumeWord re-runs the fit check on word
        // (the rest of a word after a soft-hyphen break, or a word moved to a fresh line).
        private bool wordHasSoftHyphen;
        private bool resumeWord;

        private Line Current { get; set; }

        internal Line CurrentFor(int handle) => handle == rental && rented ? Current : default;
        internal bool MoveNextFor(int handle) => handle == rental && rented && MoveNext();
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
            wrapper.state = State.Start;
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
            state = State.Done;
            current.Clear();
            word.Clear();
            atoms?.Clear();
            paragraph = null!; options = null!; resolved = null!; firstMarker = null;
            cursor = default; Current = default; space = null; cancellation = default;
            if (current.Capacity <= MaxPooledGlyphs && word.Capacity <= MaxPooledGlyphs) pooled = this;
        }

        private bool MoveNext()
        {
            switch (state)
            {
                case State.Start:
                    Validate();
                    current.Clear(); width = 0; space = null; firstLine = true;
                    resumeWord = false; wordHasSoftHyphen = false;
                    // The consumer finishes a borrowed line before MoveNext clears it.
                    // Retained page strings/paint and the PDF buffer are unchanged: this
                    // is line-at-a-time wrapping, not a streaming document serializer.
                    cursor = new GlyphReader(paragraph, options, resolved, available, cancellation);
                    hasGlyph = cursor.MoveNext();
                    break;
                case State.AfterBreak:
                    ResetLine();
                    hasGlyph = cursor.MoveNext();
                    break;
                case State.AfterPreservedSpaceWrap:
                    // Every authored space remains painted, including one at a soft wrap
                    // boundary; it starts the next line.
                    ResetLine();
                    current.Add(cursor.Current);
                    width += cursor.Current.Width;
                    hasGlyph = cursor.MoveNext();
                    break;
                case State.AfterBreakWordStart:
                    ResetLine();
                    wordIndex = 0;
                    if (PlaceBrokenWord()) return true;
                    break;
                case State.AfterBreakWordGlyph:
                    {
                        ResetLine();
                        var glyph = word[wordIndex++];
                        current.Add(glyph);
                        width += glyph.Width;
                        if (PlaceBrokenWord()) return true;
                        break;
                    }
                case State.AfterWordWrap:
                    ResetLine();
                    gap = 0;
                    // A word with soft hyphens may still be wider than a fresh line: check it again.
                    if (wordHasSoftHyphen) resumeWord = true;
                    else AppendWord();
                    break;
                case State.AfterSoftHyphenBreak:
                    ResetLine();
                    gap = 0;
                    resumeWord = true;
                    break;
                case State.Done:
                    return false;
            }
            state = State.Loop;
            while (hasGlyph || resumeWord)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!resumeWord)
                {
                if (paragraph.Style.NoWrap)
                {
                    // The paragraph deliberately has no automatic break
                    // opportunities; HTML <br> remains an explicit break.
                    if (cursor.Current.Scalar == 10) return Yield(CurrentLine(), State.AfterBreak);
                    current.Add(cursor.Current);
                    width += cursor.Current.Width;
                    hasGlyph = cursor.MoveNext();
                    continue;
                }
                if (cursor.Current.Scalar == 10) return Yield(CurrentLine(), State.AfterBreak);
                if (cursor.Current.Scalar == 32)
                {
                    if (paragraph.Style.PreserveSpaces)
                    {
                        var glyph = cursor.Current;
                        var capacity = firstLine ? available - firstIndent : available;
                        if (width + glyph.Width > capacity + 0.000001)
                        {
                            if (current.Count == 0)
                                throw new FactsPdfException("FPDF1302",
                                    "A preserved space cannot fit within the report text width.", glyph.SourceOffset);
                            // Every authored space remains painted, including one
                            // at a soft wrap boundary; never trim the existing line.
                            return Yield(CurrentLine(softWrapped: true), State.AfterPreservedSpaceWrap);
                        }
                        current.Add(glyph);
                        width += glyph.Width;
                    }
                    else space = cursor.Current;
                    hasGlyph = cursor.MoveNext();
                    continue;
                }
                word.Clear();
                wordWidth = 0d;
                wordHasSoftHyphen = false;
                var segmentWidth = 0d;
                int previous;
                do
                {
                    cancellation.ThrowIfCancellationRequested();
                    var glyph = cursor.Current;
                    word.Add(glyph);
                    wordWidth += glyph.Width;
                    if (glyph.IsSoftHyphen) { wordHasSoftHyphen = true; segmentWidth = 0; }
                    else segmentWidth += glyph.Width;
                    if (!paragraph.Style.EmergencyBreak && !paragraph.Style.BreakAll && segmentWidth > available + 0.000001)
                        throw new FactsPdfException("FPDF1302", "An unbreakable text segment exceeds the usable page width.", glyph.SourceOffset);
                    previous = glyph.Scalar;
                    hasGlyph = cursor.MoveNext();
                }
                while (hasGlyph && cursor.Current.Scalar is not (32 or 10) &&
                    !paragraph.Style.BreakAll && !CanBreak(previous, cursor.Current.Scalar));
                }
                resumeWord = false;
                gap = current.Count > 0 && space.HasValue ? space.Value.Width : 0;
                var lineAvailable = firstLine ? available - firstIndent : available;
                // An inside marker is already first-line content. A word which
                // fits the normal line but not the remaining marker line may move
                // intact to the next line. An authored text-indent alone must
                // still reject a first word that cannot fit after that indent.
                var markerOnlyWrap = firstLine && firstMarker is { Inside: true } &&
                    firstLineMarkerInset > 0 &&
                    wordWidth <= available - paragraph.Style.FirstLineIndent.Resolve(available) + 0.000001;
                // Chrome fits as much of a word as it can before a soft hyphen on the current line,
                // even after earlier words, and only then moves the word to the next line.
                if (wordHasSoftHyphen && width + gap + wordWidth > lineAvailable + 0.000001 && BreakAtSoftHyphen(lineAvailable))
                    return true;
                // Break only a token that cannot fit a fresh line, or the
                // indented first line when no earlier text can move. A fitting
                // word moves as a whole at the preceding ordinary space.
                if (paragraph.Style.EmergencyBreak &&
                    (wordWidth > available + 0.000001 ||
                     current.Count == 0 && wordWidth > lineAvailable + 0.000001 && !markerOnlyWrap))
                {
                    if (current.Count > 0) return Yield(CurrentLine(softWrapped: true), State.AfterBreakWordStart);
                    wordIndex = 0;
                    if (PlaceBrokenWord()) return true;
                    continue;
                }
                if (width + gap + wordWidth > lineAvailable + 0.000001)
                {
                    // With an inside marker and no text yet, paint the marker on this empty
                    // logical line, then wrap the whole first word using the full continuation width.
                    if (current.Count > 0 || markerOnlyWrap) return Yield(CurrentLine(softWrapped: true), State.AfterWordWrap);
                    throw new FactsPdfException("FPDF1302",
                        "The first word cannot fit after first-line indentation.", paragraph.SourceOffset);
                }
                AppendWord();
            }
            if (current.Count > 0 || firstLine && firstMarker is not null) return Yield(CurrentLine(), State.Done);
            state = State.Done;
            return false;
        }

        private bool Yield(Line line, State next)
        {
            Current = line;
            state = next;
            return true;
        }

        // Ends the line at the last soft hyphen of the word whose prefix and hyphen still fit, keeps the
        // rest of the word for the next line and returns true; false when no soft hyphen fits.
        private bool BreakAtSoftHyphen(double lineAvailable)
        {
            var prefix = 0d; var best = -1; var visible = false; Glyph bestHyphen = default;
            for (var i = 0; i < word.Count; i++)
            {
                var glyph = word[i];
                if (!glyph.IsSoftHyphen) { prefix += glyph.Width; visible = true; continue; }
                if (!visible) continue;
                var hyphen = HyphenFor(glyph, options, resolved);
                if (width + gap + prefix + hyphen.Width > lineAvailable + 0.000001) break;
                // Only a hyphen with text after it ends a line: a trailing one breaks nothing.
                for (var j = i + 1; j < word.Count; j++)
                    if (!word[j].IsSoftHyphen) { best = i; bestHyphen = hyphen; break; }
            }
            if (best < 0) return false;
            if (gap > 0) { current.Add(space!.Value); width += gap; }
            for (var i = 0; i < best; i++) { current.Add(word[i]); width += word[i].Width; }
            current.Add(bestHyphen); width += bestHyphen.Width;
            word.RemoveRange(0, best + 1);
            wordWidth = 0; wordHasSoftHyphen = false;
            foreach (var glyph in word)
            {
                wordWidth += glyph.Width;
                wordHasSoftHyphen |= glyph.IsSoftHyphen;
            }
            space = null;
            return Yield(CurrentLine(softWrapped: true), State.AfterSoftHyphenBreak);
        }

        private void AppendWord()
        {
            if (gap > 0) { current.Add(space!.Value); width += gap; }
            current.AddRange(word);
            width += wordWidth; space = null;
        }

        // Places word[wordIndex..] glyph by glyph; returns true when a full line is ready first.
        private bool PlaceBrokenWord()
        {
            for (; wordIndex < word.Count; wordIndex++)
            {
                cancellation.ThrowIfCancellationRequested();
                var glyph = word[wordIndex];
                var capacity = firstLine ? available - firstIndent : available;
                if (width + glyph.Width > capacity + 0.000001)
                {
                    if (current.Count == 0)
                        throw new FactsPdfException("FPDF1302",
                            "An individual glyph cannot fit within the report text width.", glyph.SourceOffset);
                    return Yield(CurrentLine(softWrapped: true), State.AfterBreakWordGlyph);
                }
                current.Add(glyph);
                width += glyph.Width;
            }
            space = null;
            state = State.Loop;
            return false;
        }

        private void ResetLine()
        {
            current.Clear(); width = 0; space = null;
            firstLine = false;
        }

        private void Validate()
        {
            // The opt-in first-line inset is relative to the paragraph's resolved
            // content width, not to the page or its painted border box. It belongs
            // only to the first logical line; continuation pages never re-indent.
            firstIndent = paragraph.Style.FirstLineIndent.Resolve(available) + firstLineMarkerInset;
            // Resolve the complete paragraph before reporting width errors, and
            // validate every word before yielding a line to pagination. Recording
            // a width failure (rather than throwing immediately) keeps the legacy
            // glyph -> width -> layout/budget error order without retaining lines.
            var segmentWidth = 0d;
            var previousScalar = -1;
            var tooWide = false;
            var tooWideOffset = -1;
            var firstSegment = true;
            var reader = new GlyphReader(paragraph, options, resolved, available, cancellation);
            while (reader.MoveNext())
            {
                var glyph = reader.Current;
                cancellation.ThrowIfCancellationRequested();
                if (paragraph.Style.NoWrap)
                {
                    // Collapsed spaces remain advance widths, but only an explicit
                    // <br> starts a new line. Validate the whole forced-break
                    // segment before painting any page.
                    if (glyph.Scalar == 10) { segmentWidth = 0; firstSegment = false; continue; }
                    segmentWidth += glyph.Width;
                    if (!tooWide && segmentWidth > (firstSegment ? available - firstIndent : available) + 0.000001) { tooWide = true; tooWideOffset = glyph.SourceOffset; }
                    continue;
                }
                if (glyph.Scalar is 32 or 10)
                {
                    if (paragraph.Style.PreserveSpaces && glyph.Scalar == 32 && !tooWide && glyph.Width > available + 0.000001)
                    {
                        tooWide = true; tooWideOffset = glyph.SourceOffset;
                    }
                    segmentWidth = 0;
                    previousScalar = -1;
                    continue;
                }
                if (glyph.IsSoftHyphen)
                {
                    // A word breaks at a soft hyphen when the text before it and the hyphen fit a line
                    // (the text after the last one needs no hyphen); a word is breakable when each of
                    // those stretches fits.
                    if (!paragraph.Style.EmergencyBreak && !paragraph.Style.BreakAll)
                    {
                        if (!tooWide && segmentWidth + HyphenFor(glyph, options, resolved).Width > available + 0.000001)
                        { tooWide = true; tooWideOffset = glyph.SourceOffset; }
                        segmentWidth = 0;
                        previousScalar = -1;
                    }
                    continue;
                }
                if (paragraph.Style.EmergencyBreak || paragraph.Style.BreakAll)
                {
                    // Both breaking modes use only supported resolved glyphs.
                    // A glyph wider than the fresh content line still fails.
                    if (!tooWide && glyph.Width > available + 0.000001) { tooWide = true; tooWideOffset = glyph.SourceOffset; }
                    previousScalar = glyph.Scalar;
                    continue;
                }
                segmentWidth = previousScalar < 0 || CanBreak(previousScalar, glyph.Scalar)
                    ? glyph.Width : segmentWidth + glyph.Width;
                if (!tooWide && segmentWidth > available + 0.000001) { tooWide = true; tooWideOffset = glyph.SourceOffset; }
                previousScalar = glyph.Scalar;
            }
            // Keep the legacy resource/diagnostic priority: resolve every glyph
            // before reporting indentation geometry, including for overwide boxes.
            if (!double.IsFinite(firstIndent) || firstIndent < 0 ||
                firstIndent > 0 && firstIndent >= available - 0.000001)
                throw new FactsPdfException("FPDF1302", "First-line indentation leaves no usable text width.", paragraph.SourceOffset);
            if (tooWide)
                throw new FactsPdfException("FPDF1302", "An unbreakable text segment exceeds the usable page width.", tooWideOffset);
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
            // A soft hyphen that ended no line is invisible and has no width.
            if (cursor.SawSoftHyphen) current.RemoveAll(static glyph => glyph.IsSoftHyphen);
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
            foreach (var glyph in current)
            {
                // An inline image's parent inline box contributes its own leading-adjusted
                // height, as an empty inline box does in standards mode.
                var (glyphAscent, glyphDescent) = Metrics(glyph.Style, glyph.SourceOffset);
                ascent = Math.Max(ascent, glyphAscent);
                descent = Math.Max(descent, glyphDescent);
                hasAtoms |= glyph.IsAtom;
            }
            atoms?.Clear();
            if (hasAtoms) (ascent, descent) = PlaceAtoms(ascent, descent);
            return new(current, paintedWidth, ascent + descent, ascent, descent,
                firstLine ? firstIndent : 0,
                softWrapped ? null : paragraph.Style.LastLineAlignment, hasAtoms ? atoms : null);
        }

        // CSS 2.1 §10.8: each inline image's outer box is aligned to its parent's baseline,
        // x-height or text box, which extends the line's ascent and descent; top and bottom
        // images are then aligned to the line box itself, growing it only when taller than it.
        private (double Ascent, double Descent) PlaceAtoms(double ascent, double descent)
        {
            var atoms = this.atoms ??= [];
            var deferred = false;
            foreach (var glyph in current)
            {
                if (!glyph.IsAtom) continue;
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
        if (scalar == SoftHyphen) return new(scalar, style, null, 0, sourceOffset);
        var role = FontRoles.Role(style);
        if (!resolved.TryGetValue((role, scalar), out var value))
        {
            var fonts = FontRoles.Chain(options, role, sourceOffset);
            if (fonts.Count == 0)
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

    // The hyphen a line prints where it breaks at a soft hyphen: U+2010 when the style's first font has
    // it, as Chrome chooses, else the hyphen-minus every font and the built-in font carry.
    private static Glyph HyphenFor(Glyph softHyphen, PdfOptions options,
        Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)> resolved)
    {
        var style = softHyphen.Style;
        var fonts = FontRoles.Chain(options, FontRoles.Role(style), softHyphen.SourceOffset);
        var scalar = fonts.Count > 0 && fonts[0].GlyphFor(0x2010) != 0 ? 0x2010 : '-';
        return Resolve(scalar, style, softHyphen.SourceOffset, options, resolved);
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
        /// <summary>Whether the reader has produced a soft hyphen glyph, which the line wrapper must drop or turn into a hyphen.</summary>
        internal bool SawSoftHyphen { get; private set; }

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
                    if (run is InlineImageRun image)
                    {
                        // An atomic inline: a collapsed space before it is kept, one after it
                        // collapses as after text, and it ends any ASCII word.
                        previousCarriageReturn = false;
                        insideAsciiWord = false;
                        var glyph = new Glyph(-(runIndex + 2), run.Style, null,
                            AtomGeometry(image.Image, containingWidth).OuterWidth, run.SourceOffset);
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
                    if (paragraph.Style.PreserveLineBreaks)
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
                    // Preserving modes retain ordinary U+0020 only. Never
                    // attempt to resolve unsupported control whitespace as
                    // a font glyph or accidentally output it; report a stable
                    // error before any PDF bytes replace the destination.
                    if (paragraph.Style.PreserveSpaces && (scalar is < 32 or 127))
                        throw new FactsPdfException("FPDF1305",
                            "Preformatted report text does not support tab stops or control whitespace.",
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
                    if (scalar == SoftHyphen)
                    {
                        // hyphens:none ignores a soft hyphen: no break opportunity, nothing printed.
                        if (run.Style.NoHyphens) continue;
                        SawSoftHyphen = true;
                    }
                    insideAsciiWord = wordCharacter;
                    if (scalar <= 127 && HtmlTokens.Space((char)scalar))
                    {
                        if (paragraph.Style.PreserveSpaces)
                        {
                            // Pre-wrap retains each literal U+0020 glyph and its
                            // authored style, even before/after a line break.
                            // Tabs need tab-stop layout; never collapse them
                            // silently or pretend they are literal spaces.
                            if (scalar != 32)
                                throw new FactsPdfException("FPDF1305",
                                    "Bounded pre-wrap supports literal spaces, not tabs or other control whitespace.",
                                    run.SourceOffset);
                            Current = Resolve(32, run.Style, run.SourceOffset, options, resolved);
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

    private static void CheckSimpleScalar(int scalar, int sourceOffset)
    {
        var category = Rune.GetUnicodeCategory(new Rune(scalar));
        var simpleRange = scalar is >= 32 and <= 0x052f or >= 0x1e00 and <= 0x1fff or >= 0x2000 and <= 0x2bff
            or >= 0x3000 and <= 0x30ff or >= 0x3400 and <= 0x9fff or >= 0xac00 and <= 0xd7a3
            or >= 0xf900 and <= 0xfaff or >= 0xff00 and <= 0xffef or >= 0x20000 and <= 0x323af;
        if (!simpleRange || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark or UnicodeCategory.Format or UnicodeCategory.Control
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.OtherNotAssigned)
            throw new FactsPdfException("FPDF1305", $"U+{scalar:X4} needs text processing outside this simple horizontal Unicode subset (shaping/bidi/sequence support is not implemented).", sourceOffset);
    }
    private static bool Cjk(int scalar) => scalar is >= 0x3000 and <= 0x30ff or >= 0x3400 and <= 0x9fff
        or >= 0xac00 and <= 0xd7a3 or >= 0xf900 and <= 0xfaff or >= 0xff01 and <= 0xff65 or >= 0x20000 and <= 0x323af;
    private static bool CanBreak(int previous, int next)
    {
        // CSS Text: a soft wrap opportunity before and after every atomic inline, even beside a
        // no-break space.
        if (previous < -1 || next < -1) return true;
        if (previous == 160 || next == 160) return false;
        const string opening = "（〔［｛〈《「『【〖〘〚‘“([{", closing = "、。，．？！：；）》」』】〕］｝〗〙〛’”!?;:.,)]}";
        if (previous <= 65535 && opening.Contains((char)previous) || next <= 65535 && closing.Contains((char)next)) return false;
        return Cjk(previous) || Cjk(next);
    }
}
