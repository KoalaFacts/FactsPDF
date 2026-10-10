# Block Box Tree Structural Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Preserve true nested HTML block structure through PDF layout, without changing FactsPDF's current rendered CSS subset or its PDF bytes.

**Architecture:** \`HtmlDocumentReader.ReadTree\` produces \`DocumentRoot\` containing actual \`BlockNode\` children for \`body/div/section/article/p/h1..h6\`, plus \`ParagraphNode\` leaves for explicit and anonymous text. \`html\` remains structural only. \`TextLayout.Layout(DocumentRoot)\` walks tree nodes in document order to feed the established paragraph layout until geometry/display-list support is implemented in a **separate subsequent PR**. Existing \`HtmlDocumentReader.Read\` and \`TextLayout.Layout(List<Paragraph>)\` stay as internal backward-compatibility routes for existing tests.

**Tech Stack:** .NET 10/C#, NUnit, GitHub Actions Windows/Linux/macOS, actual Linux x64 Native AOT and independent existing PDF font/stylesheet suites.

**Spec:** \`docs/superpowers/specs/2026-10-10-block-box-model-design.md\` — **only section 3 (tree) is in scope**, plus preservation of existing style/layout from sections 4–7. All width, border, padding, background, painted page fragments, display-list geometry, additional CSS properties, CSS custom layout, and new packaging remain intentionally excluded.

## Global Constraints

- **One PR at a time**, \`feat/block-box-tree\` based on \`main\` \`ee2152bdce48134e62340ee74b9188ba57aabbec\`. No stacked PRs or new adjacent feature branches.
- Keep CSS Syntax Core, selector match path, inheritance, \`!important\`, optional HTML paragraph closing, style RAWTEXT and source offsets compatible.
- Preserve \`MaxElements\`, \`MaxDepth\`, cancellation and fail-before-PDF-write; no additional browser, native libraries or runtime dependencies.
- New tree nodes are **internal implementation types**, not advertised as a public .NET/NuGet/TypeScript API.
- Do not change PDF serializer/font subset code, HTML tokenization, license/contribution files, CI workflow files or page rendering semantics in this slice.
- Pass the full existing **326 NUnit cases**, plus new tree-specific tests, all supported OS CI, real Linux Native AOT, stylesheet/PDF equivalence, CJK and font-subset evidence.
- No merging without user approval and final CI/review evidence; maintain just this PR.

## Review Focus

1. Direct text before and after a nested container must remain two anonymous leaves in proper order; nested block contents must not be hoisted.
2. Whitespace-only text between sibling blocks must not form ghost/empty paragraph nodes; an explicitly empty \`p\` must still exist.
3. A \`span\` before the first anonymous text must change the run's style, **not** the anonymous paragraph's containing block style.
4. Omitted \`p\`, \`body\` and \`html\` closing tags must flush their text leaves under the correct parent before popping.
5. CSS matching must still use real \`html/body/div/.../p\` selector ancestry, even though the \`html\` element is not itself a painted \`BlockNode\`.

## Task 1: Define the internal tree contract and tree-construction tests

**Files:** Modify \`src/FactsPDF/DocumentModel.cs\`; create \`tests/FactsPDF.Tests/BlockBoxTreeTests.cs\`.

**Interfaces:**
- \`internal abstract record BlockChild;\`
- \`internal sealed record BlockNode(string Name, TextStyle Style, int SourceOffset) : BlockChild\` with \`List<BlockChild> Children\`.
- \`internal sealed record ParagraphNode(Paragraph Paragraph, bool IsAnonymous) : BlockChild;\`
- \`internal sealed class DocumentRoot\` with \`List<BlockChild> Children\`.
- \`internal static DocumentRoot HtmlDocumentReader.ReadTree(string html, PdfOptions options, CancellationToken cancellation)\`.

- [ ] **RED:** Add tests for exact tree topology, anonymous paragraph boundaries and span styles, omitted \`p\` end tags, metadata, whitespace-only interblocks, CSS ancestry/inheritance, strict markup errors and cancellation.
- [ ] **Observe failure** on the current branch via PR CI (\`dotnet test tests/FactsPDF.Tests/FactsPDF.Tests.csproj -c Release --filter FullyQualifiedName~BlockBoxTreeTests\`).
- [ ] **GREEN:** Create tree types and replace list-append rendering reader with tree append under the active block frame. Preserve the old paragraph accessor by flattening only for internal legacy tests. Flush text before all container/paragraph closures.
- [ ] **Verify:** Above focused NUnit tests and \`dotnet test FactsPDF.slnx -c Release\`; commit exact implementation and result.

## Task 2: Consume the tree during layout and retain PDF compatibility

**Files:** Modify \`src/FactsPDF/TextLayout.cs\`, \`src/FactsPDF/PdfConverter.cs\`, \`tests/FactsPDF.Tests/BlockBoxTreeTests.cs\`.

**Interfaces:**
- New overload: \`TextLayout.Layout(DocumentRoot document, PdfOptions options, CancellationToken cancellation)\`.
- Existing overload remains \`TextLayout.Layout(List<Paragraph> paragraphs, PdfOptions o, CancellationToken cancellation)\`, both call a common layout core accepting \`IEnumerable<Paragraph>\`.
- Layout enumerates nested \`BlockNode.Children\` depth-first *without materializing a flattened list*; child iteration checks cancellation; no new geometry or CSS width.

- [ ] **RED:** Add tests comparing tree traversal to legacy paragraph layout for nested text + spans + explicit page breaks, exact placed text positions/fonts and page count, as well as real PDF conversion with nested templates.
- [ ] **Observe failure** in PR CI before adding the \`DocumentRoot\` layout overload.
- [ ] **GREEN:** Add iterative depth-first traversal, refactor old layout method into shared core and change \`PdfConverter\` to call \`ReadTree\` and tree layout overload. Do not edit PDF serialization.
- [ ] **Verify:** Focused tests, full NUnit, ASCII, Chinese PDF/font and Native AOT workflows; compare output with prior golden fixtures before/after.

## Task 3: Verify/Document/Review and merge boundary

**Files:** Create \`docs/block-box-tree.md\` and \`docs/block-box-tree-ledger.md\` within this same PR.

- [ ] **RED:** Add one content-order regression exercising deep nesting and max-depth/cancellation.
- [ ] **GREEN:** Reuse existing limits, write the actual tree/flattened layout distinction and deferred feature list; document the measured CI run SHA/test totals.
- [ ] **Verify:** Exact-head GitHub Actions Windows/Linux/macOS, Native AOT, stylesheet, syntax, subsetting and visual baseline workflows. Check merge diff excludes licensing, tokenizer and renderer extras. Ask for independent PR review, resolve important findings.
- [ ] **Merge only this PR** when code review and exact-head CI are green; validate the post-merge main SHA before beginning another feature.

### Preflight shared interfaces
Task 1 defines \`DocumentRoot/BlockNode/ParagraphNode\` consumed by Task 2. Task 2's \`Layout(DocumentRoot)\` reuses Task 1's reader; Task 3 depends on both and changes only docs/tests. Existing \`Read(List<Paragraph>)\` and \`Layout(List<Paragraph>)\` remain explicitly available for older tests.

### Excluded, deliberately
This PR **does not** implement \`width\`, \`padding\`, \`border\`, \`background-color\`, margin collapsing, box geometry, background/fill/stroke, pagination fragments, Flex/Grid, or visual parity for box drawing. The five Chrome box reference fixtures correctly continue returning current strict unsupported-property errors until later work.
