namespace FactsPDF;

internal enum TextAlignment { Left, Center, Right }
internal readonly record struct Rgb(double R, double G, double B);
internal sealed record TextStyle(double FontSize, double LineHeight, Rgb Color, TextAlignment Alignment,
    double MarginBefore = 0, double MarginAfter = 0, bool BreakBefore = false, bool BreakAfter = false);
internal sealed record TextRun(string Text, TextStyle Style, bool IsBreak = false);
internal sealed record Paragraph(TextStyle Style)
{
    public List<TextRun> Runs { get; } = [];
}
internal sealed record PlacedText(string Text, double X, double Baseline, TextStyle Style, PdfFont? Font = null);
internal sealed class LayoutPage
{
    public List<PlacedText> Runs { get; } = [];
}

// Structural box tree only. Box geometry, painting, pagination fragments and
// additional CSS properties intentionally belong to a later feature.
internal abstract record BlockChild;

internal sealed record BlockNode(string Name, TextStyle Style, int SourceOffset) : BlockChild
{
    public List<BlockChild> Children { get; } = [];
}

internal sealed record ParagraphNode(Paragraph Paragraph, bool IsAnonymous) : BlockChild;

internal sealed class DocumentRoot
{
    public List<BlockChild> Children { get; } = [];
}
