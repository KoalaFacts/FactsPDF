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
internal sealed record PlacedText(string Text, double X, double Baseline, TextStyle Style);
internal sealed class LayoutPage
{
    public List<PlacedText> Runs { get; } = [];
}
