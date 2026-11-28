using System.Text;

namespace FactsPDF;

internal static class PdfSerializer
{
    public static PdfBuffer Build(List<LayoutPage> pages, PdfOptions o, CancellationToken cancellation)
    {
        var output = new PdfBuffer(o.MaxOutputBytes, cancellation);
        var contents = ReusableBuilders.RentContent();
        var page = ReusableBuilders.RentObjects();
        var offsets = new RentedList<long>();
        offsets.Add(0);
        var images = new PdfImageWriter(pages, cancellation);
        var firstImage = 4 + 2 * pages.Count;
        // Tagged PDF: which structure element and marked-content sequence each painted item belongs to.
        var plan = TagPlan.Create(pages, o, usesCourier: pages.Exists(p => p.Runs.Count > 0), cancellation);
        var marks = plan is null ? null : new MarkedContent();
        var links = new PdfLinkAnnotations(pages, o, firstImage + images.ObjectCount, cancellation, plan);
        var firstStructure = firstImage + images.ObjectCount + links.Count;
        plan?.Finish(firstStructure);
        void Write(string text)
        {
            if (output.Length + text.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF exceeds MaxOutputBytes.");
            Span<byte> buffer = stackalloc byte[4096];
            for (var i = 0; i < text.Length;)
            {
                cancellation.ThrowIfCancellationRequested();
                var count = Math.Min(buffer.Length, text.Length - i);
                var bytes = Encoding.ASCII.GetBytes(text.AsSpan(i, count), buffer);
                output.Write(buffer[..bytes]); i += count;
            }
        }
        // The same limit check as Write, over a reused builder instead of a string per object.
        void WriteText(StringBuilder text)
        {
            if (output.Length + text.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF exceeds MaxOutputBytes.");
            PdfAscii.Write(output, text, cancellation);
        }
        void Object(int id, string body)
        {
            if (id != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
            offsets.Add(output.Position);
            Write($"{id} 0 obj\n{body}\nendobj\n");
        }
        try
        {
            Write("%PDF-1.7\n%FactsPDF\n");
            if (plan is null) Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
            else
            {
                contents.Append("<< /Type /Catalog /Pages 2 0 R");
                plan.AppendCatalogEntries(contents);
                Object(1, contents.Append(" >>").ToString());
                contents.Clear();
            }
            var alpha = new PdfAlphaStates();
            if (offsets.Count != 2) throw new InvalidOperationException("PDF object sequence is inconsistent.");
            offsets.Add(output.Position);
            page.Append("2 0 obj\n<< /Type /Pages /Count ").Append(pages.Count).Append(" /Kids [");
            for (var i = 0; i < pages.Count; i++) (i > 0 ? page.Append(' ') : page).Append(4 + 2 * i).Append(" 0 R");
            WriteText(page.Append("] >>\nendobj\n"));
            Object(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");
            for (var i = 0; i < pages.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                contents.Clear();
                alpha.Clear();
                marks?.Reset();
                if (pages[i].PaintBoxes.Count > 0) marks?.Artifact(contents);
                PdfPaintSerializer.Append(contents, pages[i].PaintBoxes, o.PageHeight, o.MaxOutputBytes, cancellation, ref alpha);
                if (plan is null) PdfPaintSerializer.AppendDiscs(contents, pages[i].Discs, o.MaxOutputBytes, cancellation, ref alpha);
                else
                    for (var d = 0; d < pages[i].Discs.Count; d++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var mcid = plan.DiscMcid(i, d);
                        if (mcid >= 0) marks!.Content(contents, mcid, plan.DiscTag(i, d)); else marks!.Artifact(contents);
                        PdfPaintSerializer.AppendDisc(contents, pages[i].Discs[d], o.MaxOutputBytes, ref alpha);
                    }
                images.Append(contents, pages[i], o, cancellation, ref alpha, plan, marks, i);
                // PDF character spacing is persistent graphics text state
                // across BT/ET. Restore zero explicitly after a tracked run,
                // but preserve byte-identical legacy PDFs that never track.
                var previousTracking = 0d;
                for (var runIndex = 0; runIndex < pages[i].Runs.Count; runIndex++)
                {
                    var run = pages[i].Runs[runIndex];
                    var color = run.Style.Color;
                    if (color.IsInvisible)
                    {
                        if (marks is not null && PdfPaintSerializer.HasTextDecoration(run)) marks.Artifact(contents);
                        PdfPaintSerializer.AppendTextDecoration(contents, run, o.MaxOutputBytes, ref alpha);
                        continue;
                    }
                    if (marks is not null)
                    {
                        var mcid = plan!.RunMcid(i, runIndex);
                        if (mcid >= 0) marks.Content(contents, mcid, plan.RunTag(i, runIndex));
                        else marks.Artifact(contents, pagination: run.Owner == RunOwner.Artifact);
                    }
                    // A translucent run selects its alpha inside q/Q, which also restores Tc.
                    var translucent = color.A < 1;
                    var tracked = previousTracking != run.Style.LetterSpacing;
                    if (!translucent) previousTracking = run.Style.LetterSpacing;
                    if (translucent) alpha.BeginPaint(contents, color);
                    contents.Append("BT /F1 ").AppendInvariant(run.Style.FontSize, Number).Append(" Tf ");
                    if (tracked) contents.AppendInvariant(run.Style.LetterSpacing, Number).Append(" Tc ");
                    contents.AppendInvariant(color.R, Number).Append(' ').AppendInvariant(color.G, Number).Append(' ').AppendInvariant(color.B, Number).Append(" rg ");
                    contents.Append("1 0 0 1 ").AppendInvariant(run.X, Number).Append(' ').AppendInvariant(run.Baseline, Number).Append(" Tm ");
                    if (run.Style.WordSpacing == 0 || !run.Text.Contains(' '))
                    {
                        contents.Append('<');
                        PdfAscii.AppendHex(contents, run.Text);
                        contents.Append("> Tj ET\n");
                    }
                    else
                    {
                        // PDF Tw does not reliably position two-byte CID space codes.
                        // Explicit TJ displacements match the shared glyph-width model.
                        contents.Append('[');
                        var start = 0;
                        for (var j = 0; j < run.Text.Length; j++)
                        {
                            if (run.Text[j] != ' ') continue;
                            contents.Append('<');
                            PdfAscii.AppendHex(contents, run.Text.AsSpan(start, j + 1 - start));
                            contents.Append("> ");
                            contents.AppendInvariant(-run.Style.WordSpacing * 1000 / run.Style.FontSize, Number).Append(' ');
                            start = j + 1;
                        }
                        if (start < run.Text.Length)
                        {
                            contents.Append('<');
                            PdfAscii.AppendHex(contents, run.Text.AsSpan(start));
                            contents.Append('>');
                        }
                        contents.Append("] TJ ET\n");
                    }
                    if (translucent) contents.Append("Q\n");
                    if (marks is not null && PdfPaintSerializer.HasTextDecoration(run)) marks.Artifact(contents);
                    PdfPaintSerializer.AppendTextDecoration(contents, run, o.MaxOutputBytes, ref alpha);
                    if (contents.Length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF content exceeds MaxOutputBytes.");
                }
                marks?.Close(contents);
                page.Clear().Append(4 + 2 * i).Append(" 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ").AppendInvariant(o.PageWidth, Number).Append(' ')
                    .AppendInvariant(o.PageHeight, Number).Append("] /Resources << /Font << /F1 3 0 R >>");
                images.AppendResources(page, pages[i], firstImage, o.MaxOutputBytes, cancellation);
                alpha.AppendResources(page);
                page.Append(" >> /Contents ").Append(5 + 2 * i).Append(" 0 R");
                plan?.AppendPageEntries(page, i, links.HasAnnotations(i));
                links.AppendPageEntry(page, i);
                page.Append(" >>\nendobj\n");
                if (4 + 2 * i != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
                offsets.Add(output.Position);
                WriteText(page);
                if (5 + 2 * i != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
                offsets.Add(output.Position);
                var length = PdfAscii.ByteCount(contents);
                if (output.Length + length > o.MaxOutputBytes) throw new FactsPdfException("FPDF1401", "PDF exceeds MaxOutputBytes.");
                WriteText(page.Clear().Append(5 + 2 * i).Append(" 0 obj\n<< /Length ").Append(length).Append(" >>\nstream\n"));
                PdfAscii.Write(output, contents, cancellation);
                Write("\nendstream\nendobj\n");
            }
            for (var i = 0; i < images.ObjectCount; i++)
            {
                if (firstImage + i != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
                offsets.Add(output.Position);
                images.WriteObject(output, i, firstImage + i, o.MaxOutputBytes, cancellation);
            }
            for (var i = 0; i < links.Count; i++)
            {
                if (firstImage + images.ObjectCount + i != offsets.Count) throw new InvalidOperationException("PDF object sequence is inconsistent.");
                offsets.Add(output.Position);
                links.AppendObject(page.Clear(), i, firstImage + images.ObjectCount + i);
                WriteText(page);
            }
            if (plan is not null)
            {
                plan.WriteObjects(output, ref offsets, page, 4, cancellation);
                output.PdfUa = plan.PdfUa;
            }
            int? infoId = null;
            var info = PdfDocumentMetadata.BuildInfo(o);
            if (info is not null)
            {
                infoId = offsets.Count;
                Object(infoId.Value, info);
            }
            var xref = output.Position;
            Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
            PdfAscii.WriteXrefEntries(output, offsets.Span);
            Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R{(infoId is null ? "" : $" /Info {infoId.Value} 0 R")} >>\nstartxref\n{xref}\n%%EOF\n");
            return output;
        }
        catch { output.Dispose(); throw; }
        finally
        {
            offsets.Release(); plan?.Release();
            ReusableBuilders.ReturnContent(contents); ReusableBuilders.ReturnObjects(page);
        }
    }

    private const string Number = "0.###";
}
