using System.Buffers;

namespace FactsPDF;

/// <summary>
/// Which structure element and marked-content sequence every painted item of a document belongs to, worked
/// out before any byte is written so both serializers share it. Content is a run of text, a list marker
/// disc or a figure image; boxes, borders, text decorations and page furniture are artifacts.
/// </summary>
/// <remarks>
/// Structure nodes are numbered from the element table: element i is node 3i, and an li or dt/dd also has
/// the two child nodes 3i+1 (LBody) and 3i+2 (Lbl); then the document node and the nodes implied for text
/// that sits directly in a grouping element (an implied P). A node is written when content lies at or below
/// it. The kids of a node are ordered by reading order: the order of first content, merging list markers
/// and images into the line order by position. All arrays come from the shared pool and are returned by
/// <see cref="Release"/>.
/// </remarks>
internal sealed partial class TagPlan
{
    private const int Facets = 3;
    internal const int ArtifactNode = -2;
    private const int Unused = int.MaxValue;

    private readonly DocumentStructure structure;
    private readonly List<LayoutPage> pages;
    private readonly double pageHeight;
    private readonly string? title;
    private readonly int elementNodes;
    private readonly int rootNode;
    private readonly int dynamicStart;

    private RentedList<int> dynamicParents;
    private int[] firstSeq;
    private int[] objectIds;
    private int[]? markerText;
    private int nextSeq;

    // Per item, with the page's first index in the Base array of the same kind.
    private int[] runNodes, runMcids, runBases;
    private int[] discNodes, discMcids, discBases;
    private int[] imageNodes, imageMcids, imageBases;
    // The node of every marked-content sequence of every page, and where each page's sequences start.
    private RentedList<int> mcidNodes;
    private RentedList<int> mcidSeqs;
    private int[] mcidBases;

    private RentedList<long> kidKeys;
    private RentedList<long> kidPayloads;
    private RentedList<int> linkNodes;
    private bool usedStrong, usedEm, usedAside;
    private int headingProblems;

    /// <summary>Whether a text run uses the built-in non-embedded font, which rules out PDF/UA.</summary>
    internal bool UsesCourier { get; }

    internal DocumentStructure Structure => structure;
    internal int PageCount => pages.Count;

    private TagPlan(List<LayoutPage> pages, DocumentStructure structure, PdfOptions options, bool usesCourier,
        CancellationToken cancellation)
    {
        this.pages = pages;
        this.structure = structure;
        pageHeight = options.PageHeight;
        title = options.Title ?? structure.Title;
        UsesCourier = usesCourier;
        elementNodes = structure.Count * Facets;
        rootNode = elementNodes;
        dynamicStart = rootNode + 1;
        int runs = 0, discs = 0, images = 0;
        foreach (var page in pages) { runs += page.Runs.Count; discs += page.Discs.Count; images += page.Images.Count; }
        var nodes = dynamicStart + runs;
        firstSeq = ArrayPool<int>.Shared.Rent(nodes);
        Array.Fill(firstSeq, Unused, 0, nodes);
        objectIds = ArrayPool<int>.Shared.Rent(nodes);
        Array.Clear(objectIds, 0, nodes);
        runNodes = Rent(runs); runMcids = Rent(runs); discNodes = Rent(discs); discMcids = Rent(discs);
        imageNodes = Rent(images); imageMcids = Rent(images);
        runBases = Rent(pages.Count + 1); discBases = Rent(pages.Count + 1); imageBases = Rent(pages.Count + 1);
        mcidBases = Rent(pages.Count + 1);
        Assign(cancellation);
    }

    private static int[] Rent(int length) => ArrayPool<int>.Shared.Rent(Math.Max(length, 1));

    /// <summary>The plan for a document, or null when it is untagged.</summary>
    internal static TagPlan? Create(List<LayoutPage> pages, PdfOptions options, bool usesCourier, CancellationToken cancellation)
        => options.Tagged && pages.Count > 0 && pages[0].Structure is { } structure
            ? new TagPlan(pages, structure, options, usesCourier, cancellation) : null;

    // The marked-content id of an item, or -1 for an artifact, nothing painted or no content.
    internal int RunMcid(int page, int run) => runMcids[runBases[page] + run];
    internal int DiscMcid(int page, int disc) => discMcids[discBases[page] + disc];
    internal int ImageMcid(int page, int image) => imageMcids[imageBases[page] + image];
    internal string RunTag(int page, int run) => TagName(runNodes[runBases[page] + run]);
    internal string DiscTag(int page, int disc) => TagName(discNodes[discBases[page] + disc]);
    internal string ImageTag(int page, int image) => TagName(imageNodes[imageBases[page] + image]);

    /// <summary>The number of marked-content sequences of a page.</summary>
    internal int McidCount(int page) => mcidBases[page + 1] - mcidBases[page];

    private static int ContentFacet(StructureRole role) => role switch
    {
        StructureRole.LI or StructureRole.Dd => 1,
        StructureRole.Dt => 2,
        _ => 0,
    };

    private int NodeOfHolder(int holder) => holder < 0 ? rootNode
        : structure.Role(holder) == StructureRole.Artifact ? ArtifactNode
        : holder * Facets + ContentFacet(structure.Role(holder));

    private int RunHolderNode(int owner)
    {
        if (owner == RunOwner.Artifact) return ArtifactNode;
        if (RunOwner.IsMarker(owner))
        {
            var item = structure.Find(RunOwner.MarkerItem(owner));
            if (item >= 0 && structure.Role(item) is StructureRole.LI) return item * Facets + 2;
            return NodeOfHolder(structure.HolderAt(RunOwner.MarkerItem(owner)));
        }
        return NodeOfHolder(structure.HolderAt(owner));
    }

    private bool IsGrouping(int node)
    {
        if (node == rootNode) return true;
        if (node >= dynamicStart) return false;
        return node % Facets == 0 && StructureRoles.IsGrouping(structure.Role(node / Facets));
    }

    private int NodeParent(int node)
    {
        if (node == rootNode) return -1;
        if (node >= dynamicStart) return dynamicParents[node - dynamicStart];
        var element = node / Facets;
        if (node % Facets != 0) return element * Facets;
        var parent = structure.Parent(element);
        return parent < 0 ? rootNode : NodeOfHolder(structure.Holder(parent));
    }

    private StructureRole NodeRole(int node)
    {
        if (node == rootNode || node >= dynamicStart || node % Facets != 0) return StructureRole.None;
        return structure.Role(node / Facets);
    }

    private string TagName(int node)
    {
        if (node < 0) return "Artifact";
        if (node == rootNode || node >= dynamicStart) return node == rootNode ? "Document" : "P";
        if (node % Facets == 1) return "LBody";
        if (node % Facets == 2) return "Lbl";
        return StructureRoles.StandardName(structure.Role(node / Facets));
    }

    private void Mark(int node, int seq)
    {
        for (; node >= 0 && firstSeq[node] == Unused; node = NodeParent(node))
        {
            firstSeq[node] = seq;
            var role = NodeRole(node);
            if (role == StructureRole.Strong) usedStrong = true;
            else if (role == StructureRole.Em) usedEm = true;
            else if (role == StructureRole.Aside) usedAside = true;
        }
    }

    private void Assign(CancellationToken cancellation)
    {
        var previousGroupHolder = -1; var previousGroupNode = -1;
        for (var p = 0; p < pages.Count; p++)
        {
            cancellation.ThrowIfCancellationRequested();
            var page = pages[p];
            runBases[p] = p == 0 ? 0 : runBases[p - 1] + pages[p - 1].Runs.Count;
            discBases[p] = p == 0 ? 0 : discBases[p - 1] + pages[p - 1].Discs.Count;
            imageBases[p] = p == 0 ? 0 : imageBases[p - 1] + pages[p - 1].Images.Count;
            mcidBases[p] = mcidNodes.Count;
            var runBase = runBases[p];

            // Nodes first, in line order: text directly in a grouping element goes into one implied P per
            // uninterrupted stretch of its runs.
            for (var r = 0; r < page.Runs.Count; r++)
            {
                var node = RunHolderNode(page.Runs[r].Owner);
                if (node >= 0 && IsGrouping(node))
                {
                    if (node != previousGroupHolder || previousGroupNode < 0)
                    {
                        dynamicParents.Add(node);
                        previousGroupNode = dynamicStart + dynamicParents.Count - 1;
                    }
                    previousGroupHolder = node;
                    node = previousGroupNode;
                }
                else { previousGroupHolder = -1; previousGroupNode = -1; }
                runNodes[runBase + r] = node;
            }
            for (var d = 0; d < page.Discs.Count; d++)
            {
                var disc = page.Discs[d];
                var item = structure.Find(disc.SourceOffset);
                var node = item >= 0 && structure.Role(item) == StructureRole.LI ? item * Facets + 2 : ArtifactNode;
                if (node >= 0)
                {
                    if (markerText is null)
                    {
                        markerText = ArrayPool<int>.Shared.Rent(elementNodes + 1);
                        Array.Clear(markerText, 0, elementNodes + 1);
                    }
                    markerText[node] = disc.Kind switch { ListMarkerKind.Circle => 0x25E6, ListMarkerKind.Square => 0x25AA, _ => 0x2022 };
                }
                discNodes[discBases[p] + d] = node;
            }
            for (var i = 0; i < page.Images.Count; i++)
            {
                var element = structure.Find(page.Images[i].SourceOffset);
                imageNodes[imageBases[p] + i] = element < 0 ? ArtifactNode
                    : structure.Role(element) == StructureRole.Artifact ? ArtifactNode
                    : NodeOfHolder(structure.Holder(element));
            }

            // Marked-content ids in the order the serializers paint: discs, images, then runs, where a run
            // continues the previous run's sequence while nothing but text lies between them.
            for (var d = 0; d < page.Discs.Count; d++)
            {
                var node = discNodes[discBases[p] + d];
                discMcids[discBases[p] + d] = node >= 0 && !page.Discs[d].Color.IsInvisible ? NewMcid(node, p) : -1;
            }
            for (var i = 0; i < page.Images.Count; i++)
            {
                var node = imageNodes[imageBases[p] + i];
                imageMcids[imageBases[p] + i] = node >= 0 ? NewMcid(node, p) : -1;
            }
            var open = -1; var openNode = -1;
            for (var r = 0; r < page.Runs.Count; r++)
            {
                var run = page.Runs[r];
                var node = runNodes[runBase + r];
                if (run.Style.Color.IsInvisible || node < 0)
                {
                    runMcids[runBase + r] = -1;
                    open = -1;
                    continue;
                }
                if (open < 0 || openNode != node) { open = NewMcid(node, p); openNode = node; }
                runMcids[runBase + r] = open;
                if (PdfPaintSerializer.HasTextDecoration(run)) open = -1;
            }
            mcidBases[p + 1] = mcidNodes.Count;

            // Reading order of the page's sequences, then the kids they make.
            SequenceItems(p, page);
        }
        headingProblems = CountHeadingProblems();
    }

    private int NewMcid(int node, int page)
    {
        mcidNodes.Add(node);
        mcidSeqs.Add(0);
        return mcidNodes.Count - 1 - mcidBases[page];
    }

    // Walks the page's items in reading order: runs in line order, with each list marker disc and image
    // placed before the first line that starts below its top. The first item of each sequence makes its kid.
    private void SequenceItems(int p, LayoutPage page)
    {
        var seen = mcidBases[p + 1] - mcidBases[p];
        Span<bool> done = seen <= 256 ? stackalloc bool[seen] : new bool[seen];
        var d = 0; var i = 0;
        for (var r = 0; r <= page.Runs.Count; r++)
        {
            var run = r < page.Runs.Count ? page.Runs[r] : null;
            while (d < page.Discs.Count && (run is null || run.Baseline < page.Discs[d].Y - 0.001))
                Sequence(discNodes[discBases[p] + d], discMcids[discBases[p] + d++], p, done);
            while (i < page.Images.Count && (run is null || ImageBefore(page.Images[i], run)))
                Sequence(imageNodes[imageBases[p] + i], imageMcids[imageBases[p] + i++], p, done);
            if (run is not null) Sequence(runNodes[runBases[p] + r], runMcids[runBases[p] + r], p, done);
        }
    }

    private bool ImageBefore(PlacedImage image, PlacedText run)
    {
        var top = pageHeight - image.Top;
        var bottom = top - image.Height;
        if (run.Baseline < bottom - 1) return true;
        return run.Baseline <= top + 1 && image.X <= run.X;
    }

    private void Sequence(int node, int mcid, int page, Span<bool> done)
    {
        if (node < 0 || mcid < 0 || done[mcid]) return;
        done[mcid] = true;
        var seq = nextSeq++;
        mcidSeqs.Span[mcidBases[page] + mcid] = seq;
        Mark(node, seq);
        kidKeys.Add(Key(node, 2L * seq));
        kidPayloads.Add(Payload(KidKind.Content, page, mcid));
    }

    private static long Key(int parent, long order) => ((long)parent << 32) | (uint)order;
    private static long Payload(KidKind kind, int page, int value) => ((long)kind << 56) | ((long)page << 32) | (uint)value;

    private enum KidKind { Content, Object, Child }

    // Headings must start at H1 and may go deeper only one level at a time (PDF/UA-1, 7.4.2).
    private int CountHeadingProblems()
    {
        var problems = 0;
        var previousLevel = 0;
        for (var element = 0; element < structure.Count; element++)
        {
            var level = StructureRoles.HeadingLevel(structure.Role(element));
            if (level == 0 || firstSeq[element * Facets] == Unused) continue;
            if (level > previousLevel + 1) problems++;
            previousLevel = level;
        }
        return problems;
    }
}
