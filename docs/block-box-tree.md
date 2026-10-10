# Block Box Tree — Structural-Only Milestone

This milestone introduces a **retained nested block tree** for FactsPDF's current documented HTML/CSS subset. It does not add new rendered CSS properties.

## Data model

\`HtmlDocumentReader.ReadTree(html, options, cancellation)\` builds an internal \`DocumentRoot\` whose children are \`BlockNode\` and \`ParagraphNode\`.

- A **\`BlockNode\`** records the actual HTML tag name, computed text style, original HTML source offset and an ordered list of child nodes. The supported block nodes are \`body\`, \`div\`, \`section\`, \`article\`, \`p\`, and \`h1\`–\`h6\`.
- A **\`ParagraphNode\`** stores the current \`Paragraph\` and an \`IsAnonymous\` flag. An explicit \`p/h1…h6\` box owns one explicit \`ParagraphNode\` (including an empty paragraph). Direct text and inline spans under a container coalesce into an anonymous paragraph. A nested block flushes that paragraph and starts a new sibling leaf when text resumes after the nested block.
- **\`html\`** participates in stylesheet selector ancestry and text-style inheritance, but is not a painted or laid-out block node in this milestone. Head, title, meta, style and \`span\` do not create block nodes. \`br\` stays a forced inline break inside the current paragraph.
- First text inside a styled \`span\` does **not** replace the anonymous paragraph's containing-block style. Span computed styles attach only to the paragraph's text runs.

This is **not** a general HTML5 DOM or browser tree-construction engine. Existing strict unsupported-tag/nesting checks, optional \`p/body/html\` closures, HTML RAWTEXT treatment, source offsets, CSS specificity and later stylesheet rules continue to apply.

## Rendering integration and backwards compatibility

The production conversion path is now:

\`\`\`text
HtmlTokens / CssStylesheets
         |
HtmlDocumentReader.ReadTree()
         |
DocumentRoot
  BlockNode ("body")
    BlockNode ("section")
      ParagraphNode (anonymous text before child box)
      BlockNode ("p")
        ParagraphNode (explicit text)
      ParagraphNode (anonymous text after child box)
         |
BlockTreeTraversal.Paragraphs()   <-- lazy, iterative depth-first visit
         |
TextLayout.LayoutCore(IEnumerable<Paragraph>)
         |
Existing ASCII / Unicode PDF serializers
\`\`\`

\`BlockTreeTraversal.Paragraphs\` iterates through the retained tree with an explicit stack of list/index frames and checks cancellation. **It does not materialize a second global paragraph list for PDF conversion.** The current text layout still flows paragraphs in document order, with its previous margins, line wrapping, page breaks, line baselines and Unicode font fallback. No box geometry is calculated or painted yet.

For existing internal tests and consumers, \`HtmlDocumentReader.Read()\` still returns \`List<Paragraph>\` by projecting the new tree; \`TextLayout.Layout(List<Paragraph>)\` remains as a compatibility overload. New actual PDF conversion uses \`ReadTree\` and \`TextLayout.Layout(DocumentRoot)\`.

## Non-goals and next feature boundary

This is **only the structural portion** of the Block Box Tree design already merged into \`main\`. It does NOT implement \`width\`, \`padding\`, borders, \`background-color\`, horizontal box placement, margin collapsing, box height, parent/child page fragments, \`box-decoration-break\`, Flexbox/Grid, full inline layout, PDF vector display lists or browser box-pixel parity.

The five existing Chrome box reference fixtures must continue to report **expected unsupported** properties under StrictPdf. Their actual Chrome/FactsPDF paired comparison is a future, separately authorized PR after the required geometry/painter features exist.

## Constraints and verification

- No new native/browser runtime, font file, installed package, network, rendering library, public API or serializer dependency.
- No license/community/commercial/contribution file edits; no package publication.
- Existing \`MaxInputCharacters\`, \`MaxElements\`, \`MaxDepth\`, \`MaxPages\` and cancellation safeguards remain. A late CSS style tag can still style an earlier paragraph.
- New \`BlockBoxTreeTests\` cover structure, source order, anonymous vs explicit paragraphs, empty text, inline styles/forced breaks, optional end tags, styles, errors, limits, cancellation, existing-vs-tree layout parity and byte-for-byte ASCII PDF serialization.

See [the exact-head implementation/verification ledger](block-box-tree-ledger.md), [the narrow implementation plan](superpowers/plans/2026-10-10-block-box-tree-structural.md), and [PR #11](https://github.com/KoalaFacts/FactsPDF/pull/11).
