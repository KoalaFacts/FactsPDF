using System.Buffers;
using System.Globalization;
using System.Text;

namespace FactsPDF;

internal sealed partial class TagPlan
{
    private int structTreeRootId, documentId, parentTreeId, metadataId;
    private bool pdfUa;

    /// <summary>Whether the file identifies itself as PDF/UA-1.</summary>
    internal bool PdfUa => pdfUa;

    /// <summary>The document title Info and XMP carry: the explicit title, else the title element's text.</summary>
    internal string? Title => title;

    /// <summary>
    /// The structure node a link annotation over a run belongs to: the Link ancestor of the run's node.
    /// </summary>
    internal int LinkNodeOfRun(int page, int run) => LinkNode(runNodes[runBases[page] + run]);
    internal int LinkNodeOfImage(int page, int image) => LinkNode(imageNodes[imageBases[page] + image]);

    private int LinkNode(int node)
    {
        for (var n = node; n >= 0; n = NodeParent(n))
            if (NodeRole(n) == StructureRole.Link) return n;
        return node;
    }

    /// <summary>
    /// Ties a link annotation to its structure node: an object reference kid of the node, and the
    /// StructParent key that finds the node again from the annotation. Returns the key.
    /// </summary>
    internal int AddLinkTarget(int node, int annotationId, int page, int mcid)
    {
        var seq = mcid >= 0 ? mcidSeqs[mcidBases[page] + mcid] : nextSeq++;
        if (node < 0 || node == rootNode) node = rootNode;
        Mark(node, seq);
        kidKeys.Add(Key(node, 2L * seq + 1));
        kidPayloads.Add(Payload(KidKind.Object, page, annotationId));
        linkNodes.Add(node);
        return pages.Count + linkNodes.Count - 1;
    }

    internal int LinkMcidOfRun(int page, int run) => runMcids[runBases[page] + run];
    internal int LinkMcidOfImage(int page, int image) => imageMcids[imageBases[page] + image];

    /// <summary>
    /// Fixes the kids of every node and assigns object numbers from <paramref name="firstId"/>; returns the
    /// next free number. Call once all link targets are added.
    /// </summary>
    internal int Finish(int firstId)
    {
        // A row that has content keeps all its cells, empty ones too: a table must stay regular.
        for (var element = 0; element < structure.Count; element++)
        {
            if (structure.Role(element) is not (StructureRole.TD or StructureRole.TH)) continue;
            var row = structure.Parent(element);
            if (row >= 0 && structure.Role(row) == StructureRole.TR && firstSeq[row * Facets] != Unused
                && firstSeq[element * Facets] == Unused)
                Mark(element * Facets, nextSeq++);
        }
        var nodes = dynamicStart + dynamicParents.Count;
        for (var node = 0; node < nodes; node++)
        {
            if (node == rootNode || firstSeq[node] == Unused) continue;
            var parent = NodeParent(node);
            kidKeys.Add(Key(parent, KidOrder(node, parent)));
            kidPayloads.Add(Payload(KidKind.Child, 0, node));
        }
        if (kidKeys.Count > 1)
            System.Array.Sort(kidKeys.Array!, kidPayloads.Array!, 0, kidKeys.Count);
        structTreeRootId = firstId;
        documentId = firstId + 1;
        objectIds[rootNode] = documentId;
        var id = firstId + 2;
        for (var node = 0; node < nodes; node++)
            if (node != rootNode && firstSeq[node] != Unused) objectIds[node] = id++;
        parentTreeId = id++;
        var hasMetadata = title is not null;
        metadataId = hasMetadata ? id++ : 0;
        pdfUa = title is not null && structure.Language is not null && structure.FiguresWithoutAlt == 0
            && !UsesCourier && headingProblems == 0;
        return id;
    }

    // Where a child node sits among its parent's kids. Table and list containers hold only structure, so
    // their children follow document order, and a list item's label comes before its body; every other
    // parent orders its children by where their first content is painted.
    private long KidOrder(int node, int parent)
    {
        if (node < elementNodes && node % Facets != 0) return node % Facets == 2 ? 0 : 2;
        if (parent >= 0 && parent < elementNodes && parent % Facets == 0 && node < elementNodes
            && structure.Role(parent / Facets) is StructureRole.Table or StructureRole.THead or StructureRole.TBody
                or StructureRole.TFoot or StructureRole.TR or StructureRole.L)
            return 2L * (node / Facets);
        return 2L * firstSeq[node];
    }

    /// <summary>Appends the catalog entries Tagged PDF adds.</summary>
    internal void AppendCatalogEntries(StringBuilder catalog)
    {
        catalog.Append(" /MarkInfo << /Marked true >> /StructTreeRoot ").Append(structTreeRootId).Append(" 0 R");
        if (structure.Language is { } language) catalog.Append(" /Lang (").Append(language).Append(')');
        if (title is not null) catalog.Append(" /ViewerPreferences << /DisplayDocTitle true >>");
        if (metadataId != 0) catalog.Append(" /Metadata ").Append(metadataId).Append(" 0 R");
    }

    /// <summary>Appends a page's StructParents key and, when it has annotations, tab order by structure.</summary>
    internal void AppendPageEntries(StringBuilder page, int index, bool hasAnnotations)
    {
        if (McidCount(index) > 0) page.Append(" /StructParents ").Append(index);
        if (hasAnnotations) page.Append(" /Tabs /S");
    }

    /// <summary>Appends the object of a link annotation's StructParent key.</summary>
    internal static void AppendStructParent(StringBuilder annotation, int key) => annotation.Append(" /StructParent ").Append(key);

    /// <summary>
    /// Writes the structure tree, the parent tree and the metadata stream as the objects reserved by
    /// <see cref="Finish"/>; <paramref name="firstPageId"/> is the object number of the first page, each
    /// page taking two numbers (page, content).
    /// </summary>
    internal void WriteObjects(PdfBuffer output, ref RentedList<long> offsets, StringBuilder text, int firstPageId,
        CancellationToken cancellation)
    {
        var nodes = dynamicStart + dynamicParents.Count;
        void Emit(ref RentedList<long> list, int id)
        {
            if (id != list.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
            list.Add(output.Position);
            PdfAscii.Write(output, text, cancellation);
        }
        text.Clear().Append(structTreeRootId).Append(" 0 obj\n<< /Type /StructTreeRoot /K ").Append(documentId)
            .Append(" 0 R /ParentTree ").Append(parentTreeId).Append(" 0 R /ParentTreeNextKey ").Append(pages.Count + linkNodes.Count);
        if (usedStrong || usedEm || usedAside)
        {
            text.Append(" /RoleMap <<");
            if (usedAside) text.Append(" /Aside /Sect");
            if (usedEm) text.Append(" /Em /Span");
            if (usedStrong) text.Append(" /Strong /Span");
            text.Append(" >>");
        }
        text.Append(" >>\nendobj\n");
        Emit(ref offsets, structTreeRootId);

        // The document first (its object number follows the tree root's), then every other node in order.
        for (var step = -1; step < nodes; step++)
        {
            var node = step < 0 ? rootNode : step;
            if (step >= 0 && (node == rootNode || firstSeq[node] == Unused)) continue;
            cancellation.ThrowIfCancellationRequested();
            var parent = node == rootNode ? structTreeRootId : objectIds[NodeParent(node)];
            text.Clear().Append(objectIds[node]).Append(" 0 obj\n<< /Type /StructElem /S /").Append(TagType(node))
                .Append(" /P ").Append(parent).Append(" 0 R");
            var first = FirstKid(node);
            var end = first;
            while (end < kidKeys.Count && (int)(kidKeys[end] >> 32) == node) end++;
            var page = -1;
            for (var k = first; k < end && page < 0; k++)
                if (Kind(kidPayloads[k]) != KidKind.Child) page = (int)((kidPayloads[k] >> 32) & 0xFFFFFF);
            if (page >= 0) text.Append(" /Pg ").Append(firstPageId + 2 * page).Append(" 0 R");
            AppendAttributes(text, node);
            text.Append(" /K [");
            for (var k = first; k < end; k++)
            {
                if (k > first) text.Append(' ');
                var payload = kidPayloads[k];
                var value = (int)(payload & 0xFFFFFFFF);
                var kidPage = (int)((payload >> 32) & 0xFFFFFF);
                switch (Kind(payload))
                {
                    case KidKind.Child: text.Append(objectIds[value]).Append(" 0 R"); break;
                    case KidKind.Content when kidPage == page: text.Append(value); break;
                    case KidKind.Content:
                        text.Append("<< /Type /MCR /Pg ").Append(firstPageId + 2 * kidPage).Append(" 0 R /MCID ").Append(value).Append(" >>");
                        break;
                    default:
                        text.Append("<< /Type /OBJR /Obj ").Append(value).Append(" 0 R /Pg ").Append(firstPageId + 2 * kidPage).Append(" 0 R >>");
                        break;
                }
            }
            text.Append("] >>\nendobj\n");
            Emit(ref offsets, objectIds[node]);
        }

        text.Clear().Append(parentTreeId).Append(" 0 obj\n<< /Nums [");
        var wrote = false;
        for (var p = 0; p < pages.Count; p++)
        {
            var count = McidCount(p);
            if (count == 0) continue;
            text.Append(wrote ? " " : "").Append(p).Append(" [");
            for (var m = 0; m < count; m++)
                text.Append(m > 0 ? " " : "").Append(objectIds[mcidNodes[mcidBases[p] + m]]).Append(" 0 R");
            text.Append(']');
            wrote = true;
        }
        for (var l = 0; l < linkNodes.Count; l++)
            text.Append(wrote || l > 0 ? " " : "").Append(pages.Count + l).Append(' ').Append(objectIds[linkNodes[l]]).Append(" 0 R");
        text.Append("] >> \nendobj\n");
        Emit(ref offsets, parentTreeId);

        if (metadataId != 0) WriteMetadata(output, ref offsets, text, cancellation);
    }

    private const string XmpHead = "<?xpacket begin=\"";
    private const string XmpOpen = "\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
        + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"";
    private const string XmpTail = "</rdf:Description></rdf:RDF></x:xmpmeta>\n<?xpacket end=\"w\"?>";

    private void WriteMetadata(PdfBuffer output, ref RentedList<long> offsets, StringBuilder text, CancellationToken cancellation)
    {
        // The XMP packet is ASCII apart from its three byte order mark, with the title's other characters
        // as numeric references, so the stream is written without an encoder.
        text.Clear().Append(XmpOpen);
        if (pdfUa) text.Append(" xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\"");
        text.Append("><dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">");
        foreach (var rune in title!.EnumerateRunes())
        {
            var value = rune.Value;
            if (value is '<' or '>' or '&' or '"' or '\'' or < 32 or > 126)
                text.Append("&#x").Append(value.ToString("X", CultureInfo.InvariantCulture)).Append(';');
            else text.Append((char)value);
        }
        text.Append("</rdf:li></rdf:Alt></dc:title>");
        if (pdfUa) text.Append("<pdfuaid:part>1</pdfuaid:part>");
        text.Append(XmpTail);
        var length = XmpHead.Length + 3 + PdfAscii.ByteCount(text);
        var header = new StringBuilder(96).Append(metadataId).Append(" 0 obj\n<< /Type /Metadata /Subtype /XML /Length ").Append(length).Append(" >>\nstream\n");
        if (metadataId != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
        offsets.Add(output.Position);
        PdfAscii.Write(output, header, cancellation);
        PdfAscii.Write(output, header.Clear().Append(XmpHead), cancellation);
        output.Write([0xEF, 0xBB, 0xBF]);
        PdfAscii.Write(output, text, cancellation);
        PdfAscii.Write(output, header.Clear().Append("\nendstream\nendobj\n"), cancellation);
    }

    // The index of the first kid of a node: the kids are sorted by their parent node.
    private int FirstKid(int node)
    {
        int low = 0, high = kidKeys.Count;
        var target = (long)node << 32;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (kidKeys[middle] < target) low = middle + 1; else high = middle;
        }
        return low;
    }

    private static KidKind Kind(long payload) => (KidKind)(payload >> 56);

    private string TagType(int node)
    {
        if (node == rootNode) return "Document";
        if (node >= dynamicStart) return "P";
        if (node % Facets == 1) return "LBody";
        if (node % Facets == 2) return "Lbl";
        return StructureRoles.Name(structure.Role(node / Facets));
    }

    private void AppendAttributes(StringBuilder text, int node)
    {
        if (node % Facets == 2 && node < rootNode && markerText is { } markers && markers[node] != 0)
        {
            text.Append(" /ActualText <FEFF");
            text.AppendHex(markers[node], 4).Append('>');
        }
        if (node >= rootNode || node % Facets != 0) return;
        var element = node / Facets;
        var role = structure.Role(element);
        if (structure.Attribute(element, "lang") is { } lang && DocumentStructure.LanguageTag(lang) is { } tag)
            text.Append(" /Lang (").Append(tag).Append(')');
        switch (role)
        {
            case StructureRole.Figure when structure.Alt(element) is { } alt && !string.IsNullOrWhiteSpace(alt):
                AppendText(text.Append(" /Alt "), alt.Trim());
                break;
            case StructureRole.Span when structure.Attribute(element, "title") is { } expansion
                && structure.Token(element).Value == "abbr" && !string.IsNullOrWhiteSpace(expansion):
                AppendText(text.Append(" /E "), expansion.Trim());
                break;
            case StructureRole.TH or StructureRole.TD:
                AppendCellAttributes(text, element, role);
                break;
        }
    }

    // A text string as a UTF-16BE hex string with a byte order mark, whatever its characters.
    internal static StringBuilder AppendText(StringBuilder text, string value)
    {
        text.Append("<FEFF");
        foreach (var unit in value) text.AppendHex(unit, 4);
        return text.Append('>');
    }

    private void AppendCellAttributes(StringBuilder text, int element, StructureRole role)
    {
        var columns = Span(structure.Attribute(element, "colspan"), 1000);
        if (role != StructureRole.TH && columns <= 1) return;
        text.Append(" /A << /O /Table");
        if (role == StructureRole.TH) text.Append(" /Scope /").Append(Scope(element));
        if (columns > 1) text.Append(" /ColSpan ").Append(columns);
        text.Append(" >>");
    }

    private static int Span(string? value, int max) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var span) ? Math.Clamp(span, 1, max) : 1;

    // A th names the cells below it (Column) or beside it (Row): a th in a thead heads its column, a th that
    // starts a row of data cells heads the row, and any other th heads a column.
    private string Scope(int element)
    {
        var row = structure.Parent(element);
        for (var ancestor = row; ancestor >= 0; ancestor = structure.Parent(ancestor))
            if (structure.Role(ancestor) == StructureRole.THead) return "Column";
        if (row < 0) return "Column";
        var first = true;
        var data = false;
        for (var sibling = row + 1; sibling < structure.End(row); sibling++)
        {
            if (structure.Parent(sibling) != row) continue;
            var siblingRole = structure.Role(sibling);
            if (sibling < element && siblingRole is StructureRole.TH or StructureRole.TD) first = false;
            if (sibling > element && siblingRole == StructureRole.TD) data = true;
        }
        return first && data ? "Row" : "Column";
    }

    /// <summary>Returns every rented array.</summary>
    internal void Release()
    {
        dynamicParents.Release(); mcidNodes.Release(); mcidSeqs.Release(); kidKeys.Release(); kidPayloads.Release(); linkNodes.Release();
        Return(ref firstSeq); Return(ref objectIds);
        if (markerText is not null) { ArrayPool<int>.Shared.Return(markerText); markerText = null; }
        Return(ref runNodes); Return(ref runMcids); Return(ref runBases);
        Return(ref discNodes); Return(ref discMcids); Return(ref discBases);
        Return(ref imageNodes); Return(ref imageMcids); Return(ref imageBases);
        Return(ref mcidBases);
    }

    private static void Return(ref int[] array)
    {
        if (array.Length > 0) ArrayPool<int>.Shared.Return(array);
        array = [];
    }
}
