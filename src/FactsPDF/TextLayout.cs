using static FactsPDF.TextLines;

namespace FactsPDF;

internal static class TextLayout
{
    private readonly record struct OpenBlock(double Top, int ContentEpoch, BoxStyle Style,
        double X, double Width, Rgb TextColor, int SourceOffset, int StartPage,
        double StartY, int Order, bool IsParagraph, bool HasOccupiedDescendant = false,
        bool HasParagraphDescendant = false, bool TopConsumed = false,
        double MarginTop = 0, double MinimumOuterHeight = 0,
        int OpeningMarginPage = -1, int OpeningPaddingPage = -1,
        double? FixedOuterHeight = null, bool AvoidBreakInside = false);

    // M6 resolves content widths and X positions from the nested tree, while
    // glyphs and pagination remain text-based. Traverse lazily without flattening
    // the box hierarchy into a second global paragraph list.
    public static List<LayoutPage> Layout(DocumentRoot document, PdfOptions o, CancellationToken cancellation)
        => LayoutCore(BlockLayout.Steps(document, o, cancellation,
            ListLayout.Prepare(document, o, cancellation)), o, cancellation);

    // Compatibility path for existing font/layout tests.
    public static List<LayoutPage> Layout(List<Paragraph> paragraphs, PdfOptions o, CancellationToken cancellation)
        => LayoutCore(paragraphs.Select(p => (BlockLayoutStep)new LayoutParagraph(p, o.Margin,
            o.PageWidth - 2 * o.Margin)), o, cancellation);

    // The element whose line height is largest on the line is the one to change.
    private static int TallestGlyphOffset(TextLines.Line line, int fallback)
    {
        var offset = fallback;
        var tallest = double.NegativeInfinity;
        var glyphs = line.Glyphs;
        for (var i = 0; i < glyphs.Count; i++)
        {
            var style = glyphs[i].Style;
            var height = style.AbsoluteLineHeight ?? style.FontSize * style.LineHeight;
            if (height > tallest && glyphs[i].SourceOffset >= 0) { tallest = height; offset = glyphs[i].SourceOffset; }
        }
        return offset;
    }

    private static List<LayoutPage> LayoutCore(IEnumerable<BlockLayoutStep> steps, PdfOptions o, CancellationToken cancellation)
    {
        var pages = new List<LayoutPage> { new() };
        var resolved = new Dictionary<(FontRole Role, int Scalar), (PdfFont? Font, double Width)>();
        var y = o.Margin; var after = 0d; var breakNext = false;
        var trimNextMargin = false;
        // Retain pending top padding only for still-open blocks. Closed empty
        // predecessors contribute trailing spacing; a spanning box receives
        // page-local decoration slices rather than repeating its top padding.
        var pendingTopPadding = 0d; var pendingClosedPadding = 0d;
        // A closed but still-deferred unpainted box can occupy real height
        // (minimum height/padding/margins) without advancing y or emitting a
        // drawing command. Keep this separate from an unconsumed opening
        // margin on a still-open ancestor; only the former justifies a page.
        var closedBoxOccupied = false;
        var opened = new List<OpenBlock>();
        var contentEpoch = 0; var paintOrder = 0;
        var displayCommands = 0;
        void EnsureDisplayCommands(int count, int sourceOffset)
        {
            if (count > o.MaxDisplayCommands - displayCommands)
                throw new FactsPdfException("FPDF1401", "PDF display command limit exceeded.", sourceOffset);
        }
        void ChargeDisplayCommands(int count, int sourceOffset = -1)
        {
            // Charge before retaining output, across all pages and both layout
            // entry points. Subtraction avoids overflowing at an int.MaxValue
            // limit; zero-area fragments retain their existing zero cost.
            EnsureDisplayCommands(count, sourceOffset);
            displayCommands += count;
        }
        // One delegate per layout, not one per placed line or block.
        Action<int, int> chargeDisplayCommands = ChargeDisplayCommands;
        Action<int, int> ensureDisplayCommands = EnsureDisplayCommands;
        void PlaceLine(Line line, double x, double baseline, int sourceOffset = -1)
            => AppendLine(line, x, baseline, pages[^1].Runs, chargeDisplayCommands, cancellation, sourceOffset);
        var bottom = o.PageHeight - o.Margin;
        // The page limit names the element whose content needed the extra page.
        void NewPage(int sourceOffset)
        {
            if (pages.Count >= o.MaxPages) throw new FactsPdfException("FPDF1303", "Page limit exceeded.", sourceOffset);
            pages.Add(new()); y = o.Margin; after = 0; closedBoxOccupied = false;
        }
        // Deferred margins on still-open ancestors must not count as page
        // content for a second explicit break on an otherwise empty page.
        // Closed empty boxes do occupy the current page even without ink.
        bool HasRetainedPageContent() => y > o.Margin + 0.000001
            || closedBoxOccupied
            || pages[^1].Runs.Count > 0
            || pages[^1].Discs.Count > 0
            || pages[^1].Images.Count > 0
            || pages[^1].PaintBoxes.Any(box => box.CommandCount > 0);

        bool CurrentPageOccupied() => HasRetainedPageContent()
            || pendingClosedPadding > 0;

        bool HasUnconsumedPanelTopMargin() => opened.Any(scope => scope.MarginTop > 0 &&
            scope.ContentEpoch == contentEpoch && !scope.TopConsumed &&
            !scope.HasOccupiedDescendant);

        double MoveUnconsumedBlocksToNewPage(bool apply = true)
        {
            // Recompute leading space from its owning scopes, not the old
            // page's aggregate. A continued painted box (and consumed scopes
            // within it) has already used its top padding on the first page.
            // A genuinely new child still moves with its own leading space.
            var leading = 0d;
            var continuedPanel = false;
            for (var i = 0; i < opened.Count; i++)
            {
                var scope = opened[i];
                var hasText = scope.ContentEpoch != contentEpoch;
                // A minimum already occupying this page cannot restart at
                // the next paragraph's explicit break merely because the
                // containing panel is unpainted. Its occupied descendants
                // must also retain consumed top padding across the break.
                var retainOrigin = scope.HasOccupiedDescendant &&
                    (scope.Style.HasLayoutPaint || scope.MinimumOuterHeight > 0 || scope.FixedOuterHeight.HasValue || continuedPanel);
                if ((scope.Style.HasLayoutPaint || scope.MinimumOuterHeight > 0 || scope.FixedOuterHeight.HasValue) &&
                    (hasText || scope.TopConsumed || scope.HasOccupiedDescendant))
                    continuedPanel = true;
                if (hasText || scope.TopConsumed) continue;
                if (retainOrigin)
                {
                    // Remember consumption even if this page contains only
                    // an empty paragraph: EndBlock must not re-add this top.
                    if (apply) opened[i] = scope with { TopConsumed = true };
                    continue;
                }
                // Preserve M6's first-text leading-space behavior for purely
                // unpainted scopes outside any continuing painted ancestor.
                // An unconsumed first fragment retains its outside top margin
                // when moving to a fresh page; a continued fragment never does.
                var firstMargin = scope.HasOccupiedDescendant ? 0 : scope.MarginTop;
                // An empty padded descendant may occupy deferred space without
                // consuming this ancestor's own first-fragment top. Only a
                // genuinely reserved minimum (marked TopConsumed at closure)
                // or actual laid-out content can consume that opening top.
                var firstPadding = scope.Top;
                if (apply) opened[i] = scope with
                {
                    StartPage = pages.Count - 1,
                    StartY = o.Margin + leading + firstMargin,
                    // StartPage can be rebased for an unpainted continuation.
                    // Already occupied opening margins/padding belong to their
                    // original page, even when the box has not emitted text.
                    OpeningMarginPage = scope.HasOccupiedDescendant
                        ? scope.OpeningMarginPage : pages.Count - 1,
                    // Still-unconsumed top padding moves even if a prior
                    // empty child already occupied its own box geometry.
                    OpeningPaddingPage = pages.Count - 1
                };
                leading += firstMargin + firstPadding;
            }
            if (apply) pendingTopPadding = leading;
            return leading;
        }
        foreach (var step in steps)
        {
            cancellation.ThrowIfCancellationRequested();
            if (step is LayoutHeadingGroup group)
            {
                var first = (BlockNode)group.Children[group.Start];
                // Moving a fresh panel retains both its outside margin and the
                // first heading's authored margin. A normally moved heading
                // still discards its top margin as before.
                var retainFirstHeadingMargin = HasUnconsumedPanelTopMargin();
                if (!breakNext && !first.Style.BreakBefore && CurrentPageOccupied() &&
                    HeadingKeep.ShouldMove(group, o, bottom - y, after, pendingTopPadding, pendingClosedPadding,
                        MoveUnconsumedBlocksToNewPage(apply: false), retainFirstHeadingMargin,
                        resolved, ensureDisplayCommands, cancellation))
                {
                    NewPage(first.SourceOffset);
                    MoveUnconsumedBlocksToNewPage();
                    pendingClosedPadding = 0;
                    trimNextMargin = !retainFirstHeadingMargin;
                }
                continue;
            }
            if (step is LayoutKeepPanel keep)
            {
                // Every eligible card contains actual glyphs. If an earlier
                // block exhausted the command budget, fail before decoding or
                // wrapping this card's future text (as ordinary paragraphs do).
                // This is a check, not a charge: real rendering counts once.
                if (displayCommands == o.MaxDisplayCommands)
                    EnsureDisplayCommands(1, keep.Node.SourceOffset);
                // Preflight only the exact bounded flat card shape validated
                // by BlockLayout. Measure with the same font/glyph wrap and
                // resolved child content widths used by actual PDF layout.
                // No extra PDF draw commands are emitted or charged here.
                var intrinsic = keep.Top + keep.Bottom;
                var previousAfter = 0d;
                var firstBefore = 0d;
                var firstChild = true;
                var usable = bottom - o.Margin;
                foreach (var child in keep.Node.Children)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var node = (BlockNode)child;
                    var keptParagraph = BlockLayout.ResolveParagraphMargins(
                        (Paragraph)node.Children[0], keep.ContentWidth, o);
                    var (_, _, available) = BlockLayout.ResolveMargins(node.BoxStyle,
                        keep.ContentWidth, node.SourceOffset);
                    var box = BlockLayout.ResolveBox(node.BoxStyle, available, node.SourceOffset,
                        keep.ContentWidth);
                    if (firstChild)
                    {
                        firstBefore = keptParagraph.Style.MarginBefore;
                        firstChild = false;
                    }
                    else intrinsic += Math.Max(previousAfter, keptParagraph.Style.MarginBefore);
                    intrinsic += box.Top + box.Bottom;
                    var sawLine = false;
                    foreach (var line in Wrap(keptParagraph, box.Inner, o, resolved, cancellation))
                    {
                        sawLine = true;
                        intrinsic += line.Height;
                        if (intrinsic > usable + 0.000001)
                            throw new FactsPdfException("FPDF1302",
                                "Kept report card is taller than the usable PDF page.",
                                keep.Node.SourceOffset);
                    }
                    if (!sawLine)
                        throw new FactsPdfException("FPDF1201",
                            "Kept report card requires nonempty text.", node.SourceOffset);
                    previousAfter = keptParagraph.Style.MarginAfter;
                }
                intrinsic += previousAfter;
                // Reserve the closing outside margin with this indivisible card.
                // Otherwise a card can fit on the old page while its
                // closing margin disappears at the next page boundary.
                var fresh = keep.MarginTop + firstBefore + intrinsic + keep.MarginBottom;
                if (fresh > usable + 0.000001)
                    throw new FactsPdfException("FPDF1302",
                        "Kept report card cannot fit intact on one page.",
                        keep.Node.SourceOffset);
                var neededHere = keep.MarginTop + Math.Max(after, firstBefore) +
                    intrinsic + keep.MarginBottom + pendingTopPadding + pendingClosedPadding;
                if (!breakNext && !keep.Node.BoxStyle.BreakBefore &&
                    CurrentPageOccupied() && y + neededHere > bottom + 0.000001)
                {
                    NewPage(keep.Node.SourceOffset);
                    MoveUnconsumedBlocksToNewPage();
                    pendingClosedPadding = 0;
                }
                continue;
            }
            if (step is BeginBlock begin)
            {
                // A previous break-after must precede a painted or
                // minimum-height successor, even when it has no text leaf.
                // Otherwise an unpainted minimum is reserved on the old page
                // and lost when the next paragraph consumes the delayed break.
                // Other unpainted containers retain paragraph-only behavior.
                // A forced section break applies to the section's first
                // border-box fragment, even when it is empty or unpainted.
                // Only real prior page content warrants an extra page: deferred
                // top margins alone cannot produce a phantom leading page.
                var forcePanelBreak = begin.Style.BreakBefore;
                if (forcePanelBreak || breakNext && (begin.Style.HasLayoutPaint ||
                    begin.MinimumOuterHeight > 0 || begin.FixedOuterHeight.HasValue || begin.PaddingTop > 0 ||
                    begin.MarginTop > 0 || begin.TrailingSpace > 0))
                {
                    // A deferred break must precede an empty box that reserves
                    // space only through its top/bottom padding or margins.
                    // An open ancestor's unconsumed margin alone, however,
                    // does not justify an otherwise blank leading PDF page.
                    if (HasRetainedPageContent())
                    {
                        NewPage(begin.SourceOffset);
                        MoveUnconsumedBlocksToNewPage();
                        pendingClosedPadding = 0;
                    }
                    // If the break is suppressed on an empty page, retain
                    // still-open ancestor margins in pendingClosedPadding.
                    // They belong to the first visible box and its paint.
                    breakNext = false;
                }
                // A void rule or other empty decorated box must fit in one
                // page fragment. Move it with its outside top margin before
                // opening it, rather than discovering an overflow at closure.
                // Blank paragraph spacing shares the deferred preceding gap.
                // Discount only that overlap, never a fixed/minimum box height.
                double ResolveRequiredHeight() => Math.Max(Math.Max(
                    begin.RequiredRuleHeight - Math.Min(after, begin.DeferredBlankParagraphMargin),
                    begin.MinimumOuterHeight), begin.FixedOuterHeight ?? 0);
                var requiredHeight = ResolveRequiredHeight();
                // An empty percentage-margin panel must keep its closing
                // outside margin with the same page-local box, even when
                // the box has no paint or minimum height. Otherwise it can
                // fit its opening but silently lose its trailing gap at a break.
                var completeEmptyFootprint = requiredHeight +
                    (begin.IndivisibleEmptyClosingMargin ?? 0);
                if (requiredHeight > 0 || begin.IndivisibleEmptyClosingMargin.HasValue)
                {
                    // Match the established paint-budget precedence before
                    // rejecting an oversized empty box. A zero-width/zero-paint
                    // preview must not manufacture a display command.
                    if (displayCommands == o.MaxDisplayCommands)
                    {
                        var preview = new PaintedBox(begin.X, o.Margin, begin.OuterWidth,
                            requiredHeight, begin.Style, begin.TextColor, paintOrder);
                        EnsureDisplayCommands(preview.CommandCount, begin.SourceOffset);
                    }
                    var ruleGap = pendingTopPadding + pendingClosedPadding + after + begin.MarginTop;
                    if (y + ruleGap + completeEmptyFootprint > bottom + 0.000001 &&
                        CurrentPageOccupied())
                    {
                        NewPage(begin.SourceOffset);
                        MoveUnconsumedBlocksToNewPage();
                        pendingClosedPadding = 0;
                        ruleGap = pendingTopPadding + begin.MarginTop;
                        // NewPage clears the preceding gap, so its overlap no
                        // longer reduces the blank panel's fresh-page footprint.
                        requiredHeight = ResolveRequiredHeight();
                        completeEmptyFootprint = requiredHeight +
                            (begin.IndivisibleEmptyClosingMargin ?? 0);
                        if (displayCommands == o.MaxDisplayCommands)
                        {
                            var preview = new PaintedBox(begin.X, o.Margin, begin.OuterWidth,
                                requiredHeight, begin.Style, begin.TextColor, paintOrder);
                            EnsureDisplayCommands(preview.CommandCount, begin.SourceOffset);
                        }
                    }
                    if (y + ruleGap + completeEmptyFootprint > bottom + 0.000001)
                        throw new FactsPdfException("FPDF1302",
                            "Empty report box and its outside spacing cannot fit on a page.", begin.SourceOffset);
                }
                // Even an empty paragraph has a bottom margin that belongs to
                // its containing block's content height, not its own border.
                if (begin.IsParagraph)
                    for (var i = 0; i < opened.Count; i++)
                        opened[i] = opened[i] with { HasParagraphDescendant = true };
                var start = y + pendingTopPadding + pendingClosedPadding + after + begin.MarginTop;
                opened.Add(new(begin.PaddingTop, contentEpoch, begin.Style,
                    begin.X, begin.OuterWidth, begin.TextColor, begin.SourceOffset,
                    pages.Count - 1, start, paintOrder++, begin.IsParagraph,
                    MarginTop: begin.MarginTop, MinimumOuterHeight: begin.MinimumOuterHeight,
                    OpeningMarginPage: pages.Count - 1,
                    OpeningPaddingPage: pages.Count - 1,
                    FixedOuterHeight: begin.FixedOuterHeight,
                    AvoidBreakInside: begin.Style.AvoidBreakInside));
                // Outside the border: only first-page placement and the
                // containing ancestor's occupied content change, never paint.
                pendingClosedPadding += begin.MarginTop;
                pendingTopPadding += begin.PaddingTop;
                continue;
            }
            if (step is EndBlock closing)
            {
                var scope = opened[^1];
                opened.RemoveAt(opened.Count - 1);
                // Preflight moves a short minimum-height panel to a fresh
                // page before opening. A panel that actually spans pages has
                // already occupied the bounded single-page minimum.
                var minimumExtra = 0d;
                // A kept report card must never silently fragment if a new
                // layout interaction exceeds its measured one-page budget.
                if (scope.AvoidBreakInside && scope.StartPage != pages.Count - 1)
                    throw new FactsPdfException("FPDF1302",
                        "Kept report card cannot fragment across pages.", scope.SourceOffset);
                // Fixed-height report panels intentionally do not fragment.
                // Reject oversize content before PDF emission rather than clipping.
                if (scope.FixedOuterHeight.HasValue && scope.StartPage != pages.Count - 1)
                    throw new FactsPdfException("FPDF1302",
                        "Fixed-height report panel cannot span pages.", scope.SourceOffset);
                if ((scope.MinimumOuterHeight > 0 || scope.FixedOuterHeight.HasValue) &&
                    scope.StartPage == pages.Count - 1)
                {
                    // A deferred preceding margin must be counted only once,
                    // including the opening geometry of nested empty panels.
                    var naturalEnd = Math.Max(scope.StartY + scope.Top,
                        y + pendingTopPadding + pendingClosedPadding + after)
                        + closing.PaddingBottom;
                    var requiredOuterHeight = Math.Max(scope.MinimumOuterHeight, scope.FixedOuterHeight ?? 0);
                    if (scope.FixedOuterHeight.HasValue &&
                        naturalEnd > scope.StartY + requiredOuterHeight + 0.000001)
                        throw new FactsPdfException("FPDF1302",
                            "Fixed-height report panel content exceeds available height.", scope.SourceOffset);
                    minimumExtra = Math.Max(0, scope.StartY + requiredOuterHeight - naturalEnd);
                }
                if (minimumExtra > 0)
                {
                    // This empty minimum has reserved real space after the
                    // ancestors' opening padding. That opening geometry now
                    // belongs to this physical page, despite the absence of a
                    // text run; do not carry it into an empty continuation.
                    for (var i = 0; i < opened.Count; i++)
                        opened[i] = opened[i] with { TopConsumed = true };
                }
                var emittedPaint = false;
                if (scope.Style.HasPaint)
                {
                    // Paragraph margins are outside their own border box,
                    // while the last child's bottom margin is within a
                    // containing block's content height.
                    var childBottomMargin = !scope.IsParagraph &&
                        (scope.ContentEpoch != contentEpoch || scope.HasParagraphDescendant ||
                         scope.MinimumOuterHeight > 0 || scope.FixedOuterHeight.HasValue)
                        ? after : 0d;
                    var finalPage = pages.Count - 1;
                    // A paint-only outline must not promote an otherwise valid
                    // empty/padded panel into an extra page. The unpainted panel
                    // can consume its deferred padding within the physical
                    // sheet's bottom margin. Permit the same geometry only
                    // while the *whole* outside stroke remains on that sheet;
                    // ordinary filled/bordered boxes keep their stricter
                    // content-area bound, including all legacy failures.
                    var physicalPaintBottom = !scope.Style.HasLayoutPaint &&
                        scope.Style.Outline.EffectiveWidth > 0
                        ? o.PageHeight - scope.Style.OutlineOffset - scope.Style.Outline.EffectiveWidth
                        : bottom;
                    // The first page's border-box origin and the last page's
                    // cursor use different page-local coordinate systems.
                    // Never use the first-page starting Y as a lower bound
                    // for the *last* fragment when this block spans pages.
                    var minimumEnd = scope.StartPage == finalPage
                        ? scope.StartY + scope.Top : o.Margin;
                    var finish = Math.Max(minimumEnd,
                        y + pendingTopPadding + pendingClosedPadding + childBottomMargin);
                    finish += closing.PaddingBottom + minimumExtra;
                    if (displayCommands == o.MaxDisplayCommands)
                    {
                        // With no allowance left, a definitely nonzero paint
                        // command must fail before later geometry validation.
                        // Reuse actual clipped/zero-area accounting rather
                        // than treating HasPaint alone as a command. At most
                        // three shapes exist: first, last and a middle slice.
                        void CheckFragment(double top, double end, bool first, bool last)
                        {
                            var candidate = new PaintedBox(scope.X, top, scope.Width,
                                Math.Max(0, end - top), scope.Style, scope.TextColor, scope.Order,
                                IsFirstFragment: first, IsLastFragment: last);
                            ChargeDisplayCommands(candidate.CommandCount, scope.SourceOffset);
                        }
                        CheckFragment(scope.StartY, scope.StartPage == finalPage ? finish : bottom,
                            true, scope.StartPage == finalPage);
                        if (scope.StartPage < finalPage)
                            CheckFragment(o.Margin, finish, false, true);
                        if (finalPage - scope.StartPage > 1)
                            CheckFragment(o.Margin, bottom, false, false);
                    }
                    if (!double.IsFinite(finish) || finish > physicalPaintBottom + 0.000001 ||
                        scope.StartY < o.Margin - 0.000001 || scope.Width < 0 ||
                        scope.StartPage > finalPage || scope.StartY > bottom + 0.000001)
                        throw new FactsPdfException("FPDF1302",
                            "Decorated box fragment exceeds the usable page area.", scope.SourceOffset);

                    // The existing M7 single-page path retains exactly the
                    // same rectangle geometry and PDF bytes. Spanning boxes
                    // instead produce one bounded, page-local slice on each
                    // occupied page. No top/bottom padding or border is
                    // duplicated on continuation pages.
                    for (var pageIndex = scope.StartPage; pageIndex <= finalPage; pageIndex++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var isFirst = pageIndex == scope.StartPage;
                        var isLast = pageIndex == finalPage;
                        var fragmentTop = isFirst ? scope.StartY : o.Margin;
                        var fragmentEnd = isLast ? finish : bottom;
                        var height = Math.Max(0, fragmentEnd - fragmentTop);
                        // An outline-only solid continuation has no lower cap.
                        // Its open vertical strokes end at the line centre plus
                        // half a stroke, not at the full joined-corner overhang.
                        // The final fragment still has its bottom cap and keeps
                        // the original full-width physical-sheet bound.
                        var fragmentPaintBottom = physicalPaintBottom;
                        if (!isLast && !scope.Style.HasLayoutPaint &&
                            scope.Style.Outline.EffectiveWidth > 0 &&
                            scope.Style.Outline.Pattern == BorderPattern.Solid)
                            fragmentPaintBottom = o.PageHeight - scope.Style.OutlineOffset -
                                scope.Style.Outline.EffectiveWidth / 2;
                        if (fragmentEnd < fragmentTop - 0.000001 ||
                            fragmentTop < o.Margin - 0.000001 ||
                            fragmentEnd > fragmentPaintBottom + 0.000001)
                            throw new FactsPdfException("FPDF1302",
                                "Page fragment geometry exceeds the page margins.", scope.SourceOffset);
                        var painted = new PaintedBox(scope.X, fragmentTop, scope.Width, height,
                            scope.Style, scope.TextColor, scope.Order,
                            IsFirstFragment: isFirst, IsLastFragment: isLast, SourceOffset: scope.SourceOffset);
                        // Outlines are ink outside the measured box. Allow an
                        // outline-only empty panel to extend past the printable
                        // bottom margin, but never off the physical PDF sheet.
                        // Check all four stroke edges, including join width:
                        // the final PDF painter has no clipping compensation.
                        var outlineWidth = painted.Style.Outline.EffectiveWidth;
                        if (painted.Width > 0 && painted.Height > 0 && outlineWidth > 0)
                        {
                            var overhang = painted.Style.OutlineOffset + outlineWidth;
                            // A joined solid corner fills the outer quadrant
                            // by a full stroke width. On continuation pages,
                            // however, the missing top/bottom cap leaves an
                            // open, butt-capped vertical segment: it extends
                            // only half a stroke past its centreline. Keep
                            // conservative full-width bounds for dashed/dotted.
                            var solid = painted.Style.Outline.Pattern == BorderPattern.Solid;
                            var topOverhang = painted.Style.OutlineOffset +
                                (solid && !painted.IsFirstFragment ? outlineWidth / 2 : outlineWidth);
                            var bottomOverhang = painted.Style.OutlineOffset +
                                (solid && !painted.IsLastFragment ? outlineWidth / 2 : outlineWidth);
                            if (painted.X - overhang < -0.000001 ||
                                painted.X + painted.Width + overhang > o.PageWidth + 0.000001 ||
                                painted.Top - topOverhang < -0.000001 ||
                                painted.Top + painted.Height + bottomOverhang > o.PageHeight + 0.000001)
                                throw new FactsPdfException("FPDF1302",
                                    "Report outline extends outside the physical PDF page.", scope.SourceOffset);
                        }
                        ChargeDisplayCommands(painted.CommandCount, scope.SourceOffset);
                        pages[pageIndex].PaintBoxes.Add(painted);
                        // Only paint generated on this page can make this page
                        // occupied. An ancestor's earlier fragment cannot.
                        if (pageIndex == finalPage)
                            emittedPaint |= painted.CommandCount > 0;
                    }
                }
                if (scope.ContentEpoch == contentEpoch && !scope.TopConsumed)
                {
                    // No laid-out text or retained first fragment consumed
                    // this top padding. Closure now makes it trailing space.
                    pendingTopPadding = Math.Max(0, pendingTopPadding - scope.Top);
                    pendingClosedPadding += scope.Top;
                }
                pendingClosedPadding += closing.PaddingBottom + closing.MarginBottom + minimumExtra;
                // An unpainted empty child can still consume page geometry
                // through padding, borders or margins. If it has already
                // occupied space on a page, a painted ancestor cannot be
                // moved intact to another page before its first text line.
                // HasOccupiedDescendant is a lifetime fact used for continuation,
                // not proof of current-page occupancy after an empty page break.
                // Opening margins/padding belong only to their opening page;
                // closing padding/margins are genuinely on this page.
                var marginOnThisPage = scope.OpeningMarginPage == pages.Count - 1;
                var paddingOnThisPage = scope.OpeningPaddingPage == pages.Count - 1;
                var occupiesSpace = emittedPaint ||
                    (paddingOnThisPage && scope.Top > 0) ||
                    (marginOnThisPage && scope.MarginTop > 0) ||
                    closing.PaddingBottom > 0 || closing.MarginBottom > 0 ||
                    minimumExtra > 0 || (scope.IsParagraph && after > 0);
                if (occupiesSpace)
                {
                    closedBoxOccupied = true;
                    for (var i = 0; i < opened.Count; i++)
                        opened[i] = opened[i] with { HasOccupiedDescendant = true };
                }
                // Page break after a panel belongs after its last paint/padding
                // fragment; the next sibling consumes it exactly once.
                if (scope.Style.BreakAfter) breakNext = true;
                continue;
            }
            if (step is LayoutImage image)
            {
                ChargeDisplayCommands(1, image.Node.SourceOffset);
                if (breakNext)
                {
                    if (HasRetainedPageContent())
                    {
                        NewPage(image.Node.SourceOffset);
                        MoveUnconsumedBlocksToNewPage();
                        pendingClosedPadding = 0;
                    }
                }
                breakNext = false;
                var imageGap = after + pendingTopPadding + pendingClosedPadding;
                if (y + imageGap + image.OuterHeight > bottom + 0.000001 && CurrentPageOccupied())
                {
                    NewPage(image.Node.SourceOffset);
                    MoveUnconsumedBlocksToNewPage();
                    imageGap = pendingTopPadding;
                }
                if (y + imageGap + image.OuterHeight > bottom + 0.000001)
                    throw new FactsPdfException("FPDF1302", "Image and required leading decoration cannot fit on a page.", image.Node.SourceOffset);
                y += imageGap;
                var painted = new PaintedBox(image.X, y, image.OuterWidth, image.OuterHeight,
                    image.Node.BoxStyle, image.Node.Style.Color, paintOrder++, SourceOffset: image.Node.SourceOffset);
                ChargeDisplayCommands(painted.CommandCount, image.Node.SourceOffset);
                if (painted.CommandCount > 0) pages[^1].PaintBoxes.Add(painted);
                pages[^1].Images.Add(new(image.Node.Image, image.ContentX, y + image.ContentTop, image.Width, image.Height));
                y += image.OuterHeight;
                pendingTopPadding = pendingClosedPadding = after = 0;
                contentEpoch++;
                for (var i = 0; i < opened.Count; i++)
                    opened[i] = opened[i] with { HasOccupiedDescendant = true };
                continue;
            }
            if (step is LayoutTable table)
            {
                var freshSpace = new TableFreshSpace(bottom - o.Margin, MoveUnconsumedBlocksToNewPage(apply: false));
                var measured = TableLayout.Measure(table, o, resolved,
                    chargeDisplayCommands, ensureDisplayCommands, cancellation, freshSpace);
                // A validated but invisible table does not occupy page space.
                // Leave deferred breaks and ancestor decoration for the next
                // visible content, just as an empty undecorated block does.
                if (measured.Height == 0 && measured.Runs.Count == 0 && measured.PaintBoxes.Count == 0 && measured.Images.Count == 0)
                    continue;
                if (breakNext)
                {
                    if (HasRetainedPageContent())
                    {
                        NewPage(table.Node.SourceOffset);
                        MoveUnconsumedBlocksToNewPage();
                        pendingClosedPadding = 0;
                    }
                }
                breakNext = false;
                var tableGap = after + pendingTopPadding + pendingClosedPadding;
                if (measured.Height > bottom - o.Margin + 0.000001)
                {
                    var header = measured.Rows[0].IsHeader ? measured.Rows[0] : null;
                    var rowIndex = header is null ? 0 : 1;
                    var first = true;
                    var affected = measured.Rows.Any(row => row.CanSplit);
                    var cursor = new TableCursor(rowIndex);
                    while (rowIndex < measured.Rows.Count)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var plan = affected ? TablePagination.FitWithContinuations(measured, cursor,
                            bottom - y - tableGap, freshSpace, first, cancellation) : null;
                        var fragment = affected ? new TablePagination.Fragment(plan!.Next.RowIndex, plan.Height)
                            : TablePagination.Fit(measured, rowIndex, bottom - y - tableGap, first, cancellation);
                        var noProgress = affected ? plan!.Parts.Count == 0 : fragment.EndRow == rowIndex;
                        if (noProgress && CurrentPageOccupied())
                        {
                            NewPage(table.Node.SourceOffset);
                            MoveUnconsumedBlocksToNewPage();
                            tableGap = pendingTopPadding;
                            plan = affected ? TablePagination.FitWithContinuations(measured, cursor,
                                bottom - y - tableGap, freshSpace, first, cancellation) : null;
                            fragment = affected ? new TablePagination.Fragment(plan!.Next.RowIndex, plan.Height)
                                : TablePagination.Fit(measured, rowIndex, bottom - y - tableGap, first, cancellation);
                            noProgress = affected ? plan!.Parts.Count == 0 : fragment.EndRow == rowIndex;
                        }
                        if (noProgress)
                            throw new FactsPdfException("FPDF1302",
                                "Table row, repeated header and required decoration cannot fit on a page.",
                                measured.Rows[rowIndex].SourceOffset);
                        y += tableGap;
                        var last = fragment.EndRow == measured.Rows.Count;
                        var tableBox = new PaintedBox(table.X, y, measured.Width,
                            last ? fragment.Height : bottom - y, table.Node.BoxStyle, table.Node.Style.Color,
                            paintOrder++, IsFirstFragment: first, IsLastFragment: last,
                            SourceOffset: table.Node.SourceOffset);
                        ChargeDisplayCommands(tableBox.CommandCount, table.Node.SourceOffset);
                        if (tableBox.CommandCount > 0) pages[^1].PaintBoxes.Add(tableBox);
                        var rowTop = y + (first ? measured.Top : 0);
                        void PlaceRow(MeasuredTableRow row, bool repeat)
                        {
                            for (var j = row.RunStart; j < row.RunStart + row.RunCount; j++)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                if (repeat) ChargeDisplayCommands(1, row.SourceOffset);
                                var run = measured.Runs[j];
                                pages[^1].Runs.Add(run with { X = table.X + run.X,
                                    Baseline = o.PageHeight - rowTop + row.Top + run.Baseline });
                            }
                            for (var j = row.BoxStart; j < row.BoxStart + row.BoxCount; j++)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                var box = measured.PaintBoxes[j];
                                if (repeat) ChargeDisplayCommands(box.CommandCount, row.SourceOffset);
                                pages[^1].PaintBoxes.Add(box with { X = table.X + box.X,
                                    Top = rowTop + box.Top - row.Top, Order = paintOrder++ });
                            }
                            for (var j = row.ImageStart; j < row.ImageStart + row.ImageCount; j++)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                if (repeat) ChargeDisplayCommands(1, row.SourceOffset);
                                var cellImage = measured.Images[j];
                                pages[^1].Images.Add(cellImage with { X = table.X + cellImage.X,
                                    Top = rowTop + cellImage.Top - row.Top });
                            }
                            rowTop += row.Height;
                        }
                        void PlaceSlice(MeasuredTableRow row, RowFragment slice)
                        {
                            // All shared-height cell slices precede every image
                            // decoration, even when short cells are exhausted.
                            foreach (var cell in row.Cells)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                var box = new PaintedBox(table.X + cell.X, rowTop, cell.Width, slice.Height,
                                    cell.BoxStyle, cell.TextColor, paintOrder++, slice.IsFirst, slice.IsLast,
                                    SourceOffset: cell.SourceOffset);
                                ChargeDisplayCommands(box.CommandCount, cell.SourceOffset);
                                if (box.CommandCount > 0) pages[^1].PaintBoxes.Add(box);
                            }
                            foreach (var selected in slice.Cells)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                var cell = row.Cells[selected.CellIndex];
                                if (selected.StartUnit == selected.EndUnit) continue;
                                var originalTop = cell.Units[selected.StartUnit].Top;
                                for (var i = selected.StartUnit; i < selected.EndUnit; i++)
                                {
                                    cancellation.ThrowIfCancellationRequested();
                                    var unit = cell.Units[i];
                                    var unitTop = rowTop + selected.ContentTop + unit.Top - originalTop;
                                    for (var j = unit.RunStart; j < unit.RunStart + unit.RunCount; j++)
                                    {
                                        cancellation.ThrowIfCancellationRequested();
                                        var run = measured.Runs[j];
                                        pages[^1].Runs.Add(run with { X = table.X + run.X,
                                            Baseline = run.Baseline + o.PageHeight - unitTop + unit.Top });
                                    }
                                    if (unit.Kind != TableCellUnitKind.Image) continue;
                                    for (var j = unit.ImageBoxStart; j < unit.ImageBoxStart + unit.ImageBoxCount; j++)
                                    {
                                        cancellation.ThrowIfCancellationRequested();
                                        var imageBox = measured.PaintBoxes[j];
                                        pages[^1].PaintBoxes.Add(imageBox with { X = table.X + imageBox.X,
                                            Top = imageBox.Top + unitTop - unit.Top, Order = paintOrder++ });
                                    }
                                    var image = measured.Images[unit.ImageIndex];
                                    pages[^1].Images.Add(image with { X = table.X + image.X,
                                        Top = image.Top + unitTop - unit.Top });
                                }
                            }
                            rowTop += slice.Height;
                        }
                        if (header is not null) PlaceRow(header, !first);
                        if (affected)
                        {
                            foreach (var part in plan!.Parts)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                if (part.Slice is { } slice) PlaceSlice(measured.Rows[part.RowIndex], slice);
                                else PlaceRow(measured.Rows[part.RowIndex], false);
                            }
                            cursor = plan.Next;
                            rowIndex = cursor.RowIndex;
                        }
                        else for (; rowIndex < fragment.EndRow; rowIndex++) PlaceRow(measured.Rows[rowIndex], false);
                        y += fragment.Height;
                        pendingTopPadding = pendingClosedPadding = after = 0;
                        contentEpoch++;
                        for (var i = 0; i < opened.Count; i++)
                            opened[i] = opened[i] with { HasOccupiedDescendant = true };
                        first = false;
                        if (!last)
                        {
                            NewPage(table.Node.SourceOffset);
                            MoveUnconsumedBlocksToNewPage();
                            tableGap = pendingTopPadding;
                        }
                    }
                    continue;
                }
                if (y + tableGap + measured.Height > bottom + 0.000001 && CurrentPageOccupied())
                {
                    NewPage(table.Node.SourceOffset);
                    MoveUnconsumedBlocksToNewPage();
                    tableGap = pendingTopPadding;
                }
                if (y + tableGap + measured.Height > bottom + 0.000001)
                    throw new FactsPdfException("FPDF1302", "Table and required leading decoration cannot fit on a page.", table.Node.SourceOffset);
                y += tableGap;
                foreach (var run in measured.Runs)
                {
                    cancellation.ThrowIfCancellationRequested();
                    pages[^1].Runs.Add(run with { X = table.X + run.X, Baseline = o.PageHeight - y + run.Baseline });
                }
                foreach (var box in measured.PaintBoxes)
                {
                    cancellation.ThrowIfCancellationRequested();
                    pages[^1].PaintBoxes.Add(box with { X = table.X + box.X, Top = y + box.Top, Order = paintOrder++ });
                }
                foreach (var cellImage in measured.Images)
                {
                    cancellation.ThrowIfCancellationRequested();
                    pages[^1].Images.Add(cellImage with { X = table.X + cellImage.X, Top = y + cellImage.Top });
                }
                y += measured.Height;
                pendingTopPadding = pendingClosedPadding = after = 0;
                contentEpoch++;
                for (var i = 0; i < opened.Count; i++)
                    opened[i] = opened[i] with { HasOccupiedDescendant = true };
                continue;
            }
            var item = (LayoutParagraph)step;
            var paragraph = item.Paragraph;
            var marginBefore = trimNextMargin ? 0 : paragraph.Style.MarginBefore;
            trimNextMargin = false;
            // Fit failures name the list item or the paragraph's element.
            var paragraphOffset = item.Marker?.SourceOffset ?? paragraph.SourceOffset;
            // Do not allocate the next paragraph's glyph/line lists after an
            // earlier paragraph or box has used the exact command allowance.
            // Blank/whitespace/break-only paragraphs can still affect geometry
            // without emitting text commands and must retain that behavior.
            if (displayCommands == o.MaxDisplayCommands && (item.Marker is not null || HasTextContent(paragraph, cancellation)))
                ChargeDisplayCommands(1, paragraphOffset);
            var insideMarkerInset = item.Marker is { Inside: true } inside
                ? inside.Width + 0.5 * inside.Style.FontSize : 0d;
            using var lines = Wrap(paragraph, item.ContentWidth, o, resolved, cancellation,
                item.Marker, insideMarkerInset).GetEnumerator();
            if (!lines.MoveNext())
            {
                // Empty painted/padded paragraphs still occupy box geometry
                // and can request page breaks, even without glyphs.
                if (breakNext || paragraph.Style.BreakBefore)
                {
                    if (HasRetainedPageContent())
                    {
                        NewPage(paragraphOffset);
                        MoveUnconsumedBlocksToNewPage();
                        pendingClosedPadding = 0;
                    }
                    // An empty paragraph consumes a preceding section boundary
                    // exactly once. Its own break-after is scheduled below.
                    breakNext = false;
                }
                if (opened.Count > 0 && opened[^1].IsParagraph &&
                    opened[^1].ContentEpoch == contentEpoch &&
                    (opened[^1].Style.HasLayoutPaint || opened[^1].Top > 0))
                {
                    var before = Math.Max(after, marginBefore);
                    opened[^1] = opened[^1] with
                    {
                        StartY = opened[^1].StartY + Math.Max(0, before - after)
                    };
                    pendingClosedPadding += before;
                    after = paragraph.Style.MarginAfter;
                }
                else
                {
                    // A blank, unpainted paragraph still contributes its outside top margin.
                    // Keep the largest deferred gap: otherwise a percentage spacer loses
                    // margin-top while a following text paragraph consumes only margin-bottom.
                    after = Math.Max(Math.Max(after, marginBefore), paragraph.Style.MarginAfter);
                }
                breakNext |= paragraph.Style.BreakAfter;
                continue;
            }
            if (breakNext || paragraph.Style.BreakBefore)
            {
                if (HasRetainedPageContent())
                {
                    NewPage(paragraphOffset);
                    MoveUnconsumedBlocksToNewPage();
                    // Discard old-page trailing closed-box spacing only when
                    // an actual page transition occurred; on a fresh page
                    // the pending space can be an open ancestor's first margin.
                    pendingClosedPadding = 0;
                }
            }
            breakNext = false;
            var gap = Math.Max(after, marginBefore) + pendingTopPadding + pendingClosedPadding;
            if (y + gap + lines.Current.Height > bottom && CurrentPageOccupied())
            {
                // Existing padded/painted scopes sometimes have deferred paint
                // even when no runs or boxes have been emitted yet. Preserve
                // their established move-to-fresh-page behaviour. Only reject
                // an unconsumed outside margin if it cannot fit on a fresh page
                // with the actual first line, before MaxPages could mask FPDF1302.
                var unconsumedMargin = HasUnconsumedPanelTopMargin();
                // A new panel's outside margin and its first child's own
                // non-collapsing top margin both belong on the fresh page.
                // Previous-page trailing spacing still stays behind.
                var firstChildMargin = unconsumedMargin ? marginBefore : 0d;
                if (unconsumedMargin &&
                    MoveUnconsumedBlocksToNewPage(apply: false) + firstChildMargin +
                        lines.Current.Height > bottom - o.Margin + 0.000001)
                    throw new FactsPdfException("FPDF1302",
                        "Report panel top margin and first line cannot fit on a fresh page.", paragraphOffset);
                NewPage(paragraphOffset);
                MoveUnconsumedBlocksToNewPage();
                // The previous paragraph's trailing gap remains behind;
                // first-fragment outside margins and padding travel with it.
                gap = pendingTopPadding + firstChildMargin;
            }
            if (gap + lines.Current.Height > bottom - o.Margin)
                throw new FactsPdfException("FPDF1302", "Paragraph margin, box padding and first line cannot fit on a page.",
                    paragraphOffset);
            if (opened.Count > 0 && opened[^1].IsParagraph &&
                opened[^1].ContentEpoch == contentEpoch)
                opened[^1] = opened[^1] with { StartY = y + gap - opened[^1].Top };
            y += gap;
            pendingTopPadding = 0;
            pendingClosedPadding = 0;
            contentEpoch++;
            var pendingMarker = item.Marker;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                var line = lines.Current;
                if (line.Height > bottom - o.Margin)
                    throw new FactsPdfException("FPDF1302", "Line height exceeds the usable page height.",
                        TallestGlyphOffset(line, paragraphOffset));
                if (y + line.Height > bottom + 0.000001) NewPage(paragraphOffset);
                var lineWidth = item.ContentWidth - line.FirstLineIndent;
                var x = item.ContentX + line.FirstLineIndent + ((line.AlignmentOverride ?? paragraph.Style.Alignment) switch
                { TextAlignment.Center => (lineWidth - line.Width) / 2,
                  TextAlignment.Right => lineWidth - line.Width, _ => 0 });
                var baseline = o.PageHeight - y - line.Ascent;
                if (pendingMarker is { } marker)
                {
                    // Inside markers consume first-line inline advance and align
                    // with that line. Outside markers retain the legacy gutter.
                    var markerRightX = marker.Inside
                        ? x - 0.5 * marker.Style.FontSize : item.MarkerRightX;
                    if (marker.Text is not null)
                    {
                        // Generated ordinal labels have their own measured marker-width
                        // box. An inherited paragraph indent belongs to item text, not
                        // to the synthetic "1." glyphs: otherwise the label can fail to
                        // fit its exactly measured width and reject a valid report.
                        var markerStyle = marker.Style with { FirstLineIndent = default };
                        var markerParagraph = new Paragraph(markerStyle, marker.SourceOffset);
                        markerParagraph.Runs.Add(new(marker.Text, markerStyle, SourceOffset: marker.SourceOffset));
                        using var markerLines = Wrap(markerParagraph, marker.Width, o, resolved, cancellation).GetEnumerator();
                        if (!markerLines.MoveNext()) throw new InvalidOperationException("Missing decimal marker line.");
                        PlaceLine(markerLines.Current, markerRightX - marker.Width, baseline, marker.SourceOffset);
                    }
                    else
                    {
                        ChargeDisplayCommands(1, marker.SourceOffset);
                        var radius = marker.Width / 2;
                        pages[^1].Discs.Add(new(markerRightX - radius,
                            baseline + 0.3 * marker.Style.FontSize, radius, marker.Style.Color, marker.Kind, marker.SourceOffset));
                    }
                    pendingMarker = null;
                }
                // Preserve the owning paragraph's source location when a
                // line exhausts the display-command budget after a vector
                // marker. Never mask the real FPDF1401 with unknown offset.
                PlaceLine(line, x, baseline, paragraph.SourceOffset);
                y += line.Height;
            }
            while (lines.MoveNext());
            after = paragraph.Style.MarginAfter; breakNext = paragraph.Style.BreakAfter;
        }
        return pages;
    }

}
