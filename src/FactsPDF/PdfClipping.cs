using System.Text;

namespace FactsPDF;

/// <summary>
/// Writes the clip paths of a page's <see cref="ClipRegion"/>s around the runs, list markers and images they
/// hold (by index range) and the paint boxes that name them. An unclipped axis gets a path far wider than any page.
/// </summary>
internal sealed class PdfClipping
{
    internal enum Kind { Runs, Discs, Images }

    private const string Number = "0.######";
    private const double Unbounded = 1_000_000;
    private readonly List<ClipRegion> regions;
    private readonly Kind kind;
    private readonly double pageHeight;
    private readonly List<int> pending = [];
    private readonly Stack<int> open = new();
    private int nextPending;

    internal PdfClipping(LayoutPage page, Kind kind, double pageHeight)
    {
        regions = page.Clips;
        this.kind = kind;
        this.pageHeight = pageHeight;
        for (var i = 0; i < regions.Count; i++)
            if (End(regions[i]) > Start(regions[i])) pending.Add(i);
        // Opened outermost first: by start, then the longer range before the shorter one it holds.
        pending.Sort((a, b) =>
        {
            var byStart = Start(regions[a]).CompareTo(Start(regions[b]));
            return byStart != 0 ? byStart : End(regions[b]).CompareTo(End(regions[a]));
        });
    }

    internal bool Any => pending.Count > 0;

    private int Start(ClipRegion region) => kind switch
    { Kind.Runs => region.RunStart, Kind.Discs => region.DiscStart, _ => region.ImageStart };

    private int End(ClipRegion region) => kind switch
    { Kind.Runs => region.RunEnd, Kind.Discs => region.DiscEnd, _ => region.ImageEnd };

    /// <summary>Closes the regions that ended before item <paramref name="index"/> and opens those that start there.</summary>
    internal void Enter(StringBuilder output, int index)
    {
        while (open.Count > 0 && End(regions[open.Peek()]) <= index)
        {
            open.Pop();
            output.Append("Q\n");
        }
        while (nextPending < pending.Count && Start(regions[pending[nextPending]]) <= index)
        {
            var id = pending[nextPending++];
            if (End(regions[id]) <= index) continue;
            open.Push(id);
            Path(output, regions[id], pageHeight);
        }
    }

    internal void Finish(StringBuilder output)
    {
        while (open.Count > 0)
        {
            open.Pop();
            output.Append("Q\n");
        }
    }

    /// <summary>Opens <c>q</c> and the clip path of one region.</summary>
    internal static void Path(StringBuilder output, ClipRegion region, double pageHeight)
    {
        var x = region.ClipX ? region.X : -Unbounded;
        var width = region.ClipX ? region.Width : 2 * Unbounded;
        var top = region.ClipY ? region.Top : -Unbounded;
        var height = region.ClipY ? region.Height : 2 * Unbounded;
        output.Append("q ").AppendInvariant(x, Number).Append(' ').AppendInvariant(pageHeight - top - height, Number).Append(' ')
            .AppendInvariant(width, Number).Append(' ').AppendInvariant(height, Number).Append(" re W n\n");
    }

    /// <summary>Moves the open clips of a paint box sequence from the current region to <paramref name="target"/> (0 for none).</summary>
    internal static void MoveBoxClip(StringBuilder output, IReadOnlyList<ClipRegion> clips, List<int> chain, int target, double pageHeight)
    {
        var wanted = new List<int>();
        for (var id = target; id != 0; id = clips[id - 1].Parent) wanted.Insert(0, id);
        var common = 0;
        while (common < chain.Count && common < wanted.Count && chain[common] == wanted[common]) common++;
        for (var i = chain.Count - 1; i >= common; i--) { output.Append("Q\n"); chain.RemoveAt(i); }
        for (var i = common; i < wanted.Count; i++)
        {
            Path(output, clips[wanted[i] - 1], pageHeight);
            chain.Add(wanted[i]);
        }
    }
}
