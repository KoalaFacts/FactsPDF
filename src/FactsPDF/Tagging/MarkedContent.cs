using System.Text;

namespace FactsPDF;

/// <summary>
/// The marked-content sequence open in one page's content stream while a serializer paints: a structure
/// element's content (BDC with its MCID) or an artifact, never nested. Switching closes the open sequence;
/// asking for the one already open writes nothing, so runs of one element share a sequence.
/// </summary>
internal sealed class MarkedContent
{
    private const int None = -3, ArtifactOpen = -1, PaginationOpen = -2;
    private int open = None;

    /// <summary>Starts a page: nothing is open.</summary>
    internal void Reset() => open = None;

    internal void Content(StringBuilder content, int mcid, string tag)
    {
        if (open == mcid) return;
        Close(content);
        content.Append('/').Append(tag).Append(" <</MCID ").Append(mcid).Append(">> BDC\n");
        open = mcid;
    }

    /// <summary>Opens an artifact sequence: page furniture is marked as pagination.</summary>
    internal void Artifact(StringBuilder content, bool pagination = false)
    {
        var kind = pagination ? PaginationOpen : ArtifactOpen;
        if (open == kind) return;
        Close(content);
        content.Append(pagination ? "/Artifact <</Type /Pagination>> BDC\n" : "/Artifact BMC\n");
        open = kind;
    }

    internal void Close(StringBuilder content)
    {
        if (open == None) return;
        content.Append("EMC\n");
        open = None;
    }
}
