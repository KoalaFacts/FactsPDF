using System.Text;

namespace FactsPDF;

/// <summary>Writes one view of a page (its boxes, list markers, images and runs, no effect regions) and
/// returns the character spacing (Tc) it leaves set.</summary>
internal delegate double PdfLayerWriter(StringBuilder output, LayoutPage view, ref PdfAlphaStates alpha);

/// <summary>
/// Paints the stacking contexts of a page's <see cref="EffectRegion"/>s. The page is split into the content
/// outside every outermost region, painted first in the usual layers, and one view per outermost region,
/// painted after it in tree order (CSS 2 Appendix E, step 8) inside the clips around the region. A region
/// with a transform is enclosed in its cm; one with opacity below 1 is a transparency group form XObject
/// painted with the opacity as its constant alpha, as Chrome's PDF backend writes a layer with opacity.
/// Nested regions are views of their region's view, recursively. Pages without regions are written as
/// before, byte for byte.
/// </summary>
internal sealed class PdfEffects
{
    private const string Number = "0.######";
    private readonly int firstId;
    private int nextId;
    private readonly List<(int Id, int Page, string Body)> forms = [];
    private readonly List<string?> resources = [];

    /// <summary>The transparency group objects, numbered from <paramref name="firstId"/>.</summary>
    internal PdfEffects(List<LayoutPage> pages, int firstId)
    {
        this.firstId = nextId = firstId;
        foreach (var page in pages)
            foreach (var region in page.Effects)
                if (region.Alpha < 255) ObjectCount++;
    }

    internal int ObjectCount { get; }

    /// <summary>Writes a whole page's content: the layers, then its stacking contexts. The ids of the
    /// transparency groups it uses are added to <paramref name="pageForms"/>.</summary>
    internal void Append(StringBuilder output, LayoutPage page, int pageIndex, PdfOptions o, PdfLayerWriter layer,
        ref PdfAlphaStates alpha, List<int> pageForms)
    {
        if (page.Effects.Count == 0)
        {
            layer(output, page, ref alpha);
            return;
        }
        var (outer, groups) = Split(page);
        var tracking = layer(output, outer, ref alpha);
        if (tracking != 0) output.Append("0 Tc\n");
        var chain = new List<int>();
        foreach (var group in groups)
        {
            PdfClipping.MoveBoxClip(output, outer.Clips, chain, group.Clip, o.PageHeight);
            var region = group.Region;
            // A transform that flattens the box to a line or a point (scale(0), rotateY(90deg)) paints nothing.
            if (region.Transform is { IsSingular: true }) continue;
            output.Append("q ");
            if (region.Transform is { } m)
                output.AppendInvariant(m.A, Number).Append(' ').AppendInvariant(m.B, Number).Append(' ')
                    .AppendInvariant(m.C, Number).Append(' ').AppendInvariant(m.D, Number).Append(' ')
                    .AppendInvariant(m.E, Number).Append(' ').AppendInvariant(m.F, Number).Append(" cm ");
            if (region.Alpha < 255)
            {
                alpha.Select(output, new Rgb(0, 0, 0, region.Alpha / 255d));
                var id = nextId++;
                pageForms.Add(id);
                output.Append("/Fx").Append(id).Append(" Do Q\n");
                var body = new StringBuilder();
                Append(body, group.Inner, pageIndex, o, layer, ref alpha, pageForms);
                if (body.Length > o.MaxOutputBytes)
                    throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.", region.SourceOffset);
                forms.Add((id, pageIndex, body.ToString()));
            }
            else
            {
                output.Append('\n');
                Append(output, group.Inner, pageIndex, o, layer, ref alpha, pageForms);
                output.Append("Q\n");
            }
            if (output.Length > o.MaxOutputBytes)
                throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.", region.SourceOffset);
        }
        PdfClipping.MoveBoxClip(output, outer.Clips, chain, 0, o.PageHeight);
    }

    /// <summary>Appends the /XObject entries of a page's transparency groups.</summary>
    internal static void AppendFormEntries(StringBuilder output, List<int> pageForms)
    {
        foreach (var id in pageForms) output.Append(" /Fx").Append(id).Append(' ').Append(id).Append(" 0 R");
    }

    /// <summary>Keeps the resource dictionary of a page whose transparency groups use it too.</summary>
    internal void SetResources(int pageIndex, string dictionary)
    {
        while (resources.Count <= pageIndex) resources.Add(null);
        resources[pageIndex] = dictionary;
    }

    /// <summary>Writes the transparency group objects in id order, right after the objects before
    /// <c>firstId</c>, recording each object's offset.</summary>
    internal void WriteObjects(Stream output, List<long> offsets, PdfOptions o, CancellationToken cancellation)
    {
        if (forms.Count != ObjectCount || nextId != firstId + ObjectCount)
            throw new InvalidOperationException("PDF transparency group count is inconsistent.");
        forms.Sort((a, b) => a.Id.CompareTo(b.Id));
        var text = new StringBuilder();
        foreach (var (id, page, body) in forms)
        {
            cancellation.ThrowIfCancellationRequested();
            if (id != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
            offsets.Add(output.Position);
            text.Clear().Append(id).Append(" 0 obj\n<< /Type /XObject /Subtype /Form /BBox [")
                .AppendInvariant(-o.PageWidth, Number).Append(' ').AppendInvariant(-o.PageHeight, Number).Append(' ')
                .AppendInvariant(2 * o.PageWidth, Number).Append(' ').AppendInvariant(2 * o.PageHeight, Number)
                .Append("] /Group << /S /Transparency >> /Resources ").Append(resources[page])
                .Append(" /Length ").Append(Encoding.ASCII.GetByteCount(body)).Append(" >>\nstream\n").Append(body)
                .Append("\nendstream\nendobj\n");
            if (output.Length + text.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF exceeds MaxOutputBytes.");
            PdfAscii.Write(output, text, cancellation);
        }
    }

    internal sealed record Group(EffectRegion Region, LayoutPage Inner, int Clip);

    /// <summary>
    /// The view of the page outside its outermost regions, and one view per outermost region in tree
    /// order, with the number (in the outer view) of the innermost clip around it.
    /// </summary>
    internal static (LayoutPage Outer, List<Group> Groups) Split(LayoutPage page)
    {
        var effects = page.Effects;
        var clips = page.Clips;
        var top = new List<int>();
        for (var i = 0; i < effects.Count; i++) if (effects[i].Parent == 0) top.Add(i);
        top.Sort((a, b) => effects[a].Order.CompareTo(effects[b].Order));
        var runs = Mask(page.Runs.Count, true); var discs = Mask(page.Discs.Count, true);
        var images = Mask(page.Images.Count, true); var boxes = Mask(page.PaintBoxes.Count, true);
        var outerClips = Mask(clips.Count, true);
        foreach (var g in top)
        {
            var region = effects[g];
            Clear(runs, region.RunStart, region.RunEnd); Clear(discs, region.DiscStart, region.DiscEnd);
            Clear(images, region.ImageStart, region.ImageEnd); Clear(boxes, region.BoxStart, region.BoxEnd);
            for (var c = 0; c < clips.Count; c++) if (Inside(clips[c], c, region)) outerClips[c] = false;
        }
        var (outer, outerMap) = View(page, runs, discs, images, boxes, outerClips, []);
        var groups = new List<Group>(top.Count);
        foreach (var g in top)
        {
            var region = effects[g];
            var innerClips = new bool[clips.Count];
            for (var c = 0; c < clips.Count; c++) innerClips[c] = Inside(clips[c], c, region);
            var descendants = new List<int>();
            for (var i = 0; i < effects.Count; i++)
                for (var parent = effects[i].Parent; parent != 0; parent = effects[parent - 1].Parent)
                    if (parent - 1 == g) { descendants.Add(i); break; }
            var (inner, _) = View(page, Range(page.Runs.Count, region.RunStart, region.RunEnd),
                Range(page.Discs.Count, region.DiscStart, region.DiscEnd), Range(page.Images.Count, region.ImageStart, region.ImageEnd),
                Range(page.PaintBoxes.Count, region.BoxStart, region.BoxEnd), innerClips, descendants);
            var enclosing = Enclosing(page, region);
            groups.Add(new(region, inner, MapClip(clips, outerMap, enclosing)));
        }
        return (outer, groups);
    }

    private static bool[] Mask(int count, bool value)
    {
        var mask = new bool[count];
        if (value) Array.Fill(mask, true);
        return mask;
    }

    private static bool[] Range(int count, int start, int end)
    {
        var mask = new bool[count];
        for (var i = start; i < end; i++) mask[i] = true;
        return mask;
    }

    private static void Clear(bool[] mask, int start, int end)
    {
        for (var i = start; i < end; i++) mask[i] = false;
    }

    // A clip made before the region (by a box inside it) and starting inside it.
    private static bool Inside(ClipRegion clip, int index, EffectRegion region) =>
        index < region.ClipCount && clip.RunStart >= region.RunStart && clip.DiscStart >= region.DiscStart
        && clip.ImageStart >= region.ImageStart && clip.RunEnd <= region.RunEnd && clip.DiscEnd <= region.DiscEnd
        && clip.ImageEnd <= region.ImageEnd;

    // The innermost clip around a region (a clip made after it whose ranges hold the region's), by number, or 0.
    private static int Enclosing(LayoutPage page, EffectRegion region)
    {
        var clips = page.Clips;
        var content = region.RunEnd > region.RunStart || region.DiscEnd > region.DiscStart || region.ImageEnd > region.ImageStart;
        if (content)
        {
            for (var c = region.ClipCount; c < clips.Count; c++)
            {
                var clip = clips[c];
                if ((region.RunEnd == region.RunStart || clip.RunStart <= region.RunStart && region.RunEnd <= clip.RunEnd)
                    && (region.DiscEnd == region.DiscStart || clip.DiscStart <= region.DiscStart && region.DiscEnd <= clip.DiscEnd)
                    && (region.ImageEnd == region.ImageStart || clip.ImageStart <= region.ImageStart && region.ImageEnd <= clip.ImageEnd))
                    return c + 1;
            }
            return 0;
        }
        // Only paint boxes: an enclosing clip named a box of the region when it was made (TextLayout.ApplyClip).
        for (var b = region.BoxStart; b < region.BoxEnd; b++)
        {
            var id = page.PaintBoxes[b].Clip;
            while (id != 0 && id - 1 < region.ClipCount) id = clips[id - 1].Parent;
            if (id != 0) return id;
        }
        return 0;
    }

    // The number in a view of a clip of the page, or of the nearest enclosing clip the view keeps; 0 for none.
    private static int MapClip(List<ClipRegion> clips, int[] map, int id)
    {
        while (id != 0 && map[id - 1] == 0) id = clips[id - 1].Parent;
        return id == 0 ? 0 : map[id - 1];
    }

    private static int[] Prefix(bool[] keep)
    {
        var prefix = new int[keep.Length + 1];
        for (var i = 0; i < keep.Length; i++) prefix[i + 1] = prefix[i] + (keep[i] ? 1 : 0);
        return prefix;
    }

    // A page holding the kept items, the kept clips (renumbered, their ranges and parents remapped) and
    // the given effect regions (likewise remapped; a parent outside them becomes 0).
    private static (LayoutPage View, int[] ClipMap) View(LayoutPage page, bool[] runs, bool[] discs, bool[] images, bool[] boxes,
        bool[] keepClips, List<int> regions)
    {
        var view = new LayoutPage();
        var clips = page.Clips;
        var map = new int[clips.Count];
        var kept = 0;
        for (var c = 0; c < clips.Count; c++) if (keepClips[c]) map[c] = ++kept;
        var runPrefix = Prefix(runs); var discPrefix = Prefix(discs); var imagePrefix = Prefix(images); var boxPrefix = Prefix(boxes);
        for (var c = 0; c < clips.Count; c++)
        {
            if (!keepClips[c]) continue;
            var clip = clips[c];
            var copy = clip with
            {
                RunStart = runPrefix[clip.RunStart], RunEnd = runPrefix[clip.RunEnd],
                DiscStart = discPrefix[clip.DiscStart], DiscEnd = discPrefix[clip.DiscEnd],
                ImageStart = imagePrefix[clip.ImageStart], ImageEnd = imagePrefix[clip.ImageEnd],
            };
            copy.Parent = MapClip(clips, map, clip.Parent);
            view.WritableClips.Add(copy);
        }
        for (var i = 0; i < runs.Length; i++) if (runs[i]) view.Runs.Add(page.Runs[i]);
        for (var i = 0; i < discs.Length; i++) if (discs[i]) view.Discs.Add(page.Discs[i]);
        for (var i = 0; i < images.Length; i++) if (images[i]) view.Images.Add(page.Images[i]);
        for (var i = 0; i < boxes.Length; i++)
        {
            if (!boxes[i]) continue;
            var box = page.PaintBoxes[i];
            var clip = MapClip(clips, map, box.Clip);
            view.PaintBoxes.Add(clip == box.Clip ? box : box with { Clip = clip });
        }
        foreach (var index in regions)
        {
            var region = page.Effects[index];
            var clipCount = 0;
            for (var c = 0; c < region.ClipCount && c < clips.Count; c++) if (keepClips[c]) clipCount++;
            var copy = region with
            {
                RunStart = runPrefix[region.RunStart], RunEnd = runPrefix[region.RunEnd],
                DiscStart = discPrefix[region.DiscStart], DiscEnd = discPrefix[region.DiscEnd],
                ImageStart = imagePrefix[region.ImageStart], ImageEnd = imagePrefix[region.ImageEnd],
                BoxStart = boxPrefix[region.BoxStart], BoxEnd = boxPrefix[region.BoxEnd],
                ClipCount = clipCount,
            };
            copy.Parent = region.Parent != 0 && regions.IndexOf(region.Parent - 1) is var at and >= 0 ? at + 1 : 0;
            view.WritableEffects.Add(copy);
        }
        return (view, map);
    }
}
