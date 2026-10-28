"""Run the native FactsPDF CLI on stylesheet and independently handwritten inline fixtures.
Poppler/qpdf inspect outputs only. No browser or external CSS engine generates either PDF.
Artifacts contain a public test document, not user data, and never standalone font files.
"""
import hashlib
from html.parser import HTMLParser
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET


def run(*args):
    result = subprocess.run([str(arg) for arg in args], capture_output=True, timeout=120)
    if result.returncode:
        raise RuntimeError(f'{args[0]} exited {result.returncode}: ' + result.stderr.decode('utf-8', errors='replace'))
    return result.stdout


class VisibleText(HTMLParser):
    def __init__(self):
        super().__init__(); self.parts = []; self.hidden = 0
    def handle_starttag(self, tag, attrs):
        if tag in ('head', 'style'): self.hidden += 1
    def handle_endtag(self, tag):
        if tag in ('head', 'style'): self.hidden -= 1
    def handle_data(self, text):
        if not self.hidden: self.parts.append(text)


def main():
    exe = Path(sys.argv[1]).resolve()
    fonts = [Path('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'), Path('/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf')]
    inputs = [Path('examples/stylesheet-document.html'), Path('examples/stylesheet-inline-oracle.html')]
    output = Path('artifacts/stylesheets'); output.mkdir(parents=True, exist_ok=True)
    pdfs = [output / 'stylesheet-document.pdf', output / 'inline-oracle.pdf']
    extracted = []; expected = []
    for html, pdf in zip(inputs, pdfs):
        parser = VisibleText(); parser.feed(html.read_text(encoding='utf-8')); expected.append(''.join(''.join(parser.parts).split()))
        args = [exe, html, pdf, '--subset-fonts']
        for font in fonts: args += ['--font', font]
        run(*args)
        print(run('qpdf', '--check', pdf).decode())
        info = run('pdfinfo', pdf).decode()
        if not re.search(r'^Pages:\s+2$', info, re.M): raise AssertionError(info)
        rows = run('pdffonts', pdf).decode().splitlines()[2:]
        if len(rows) != 2 or not all('CID TrueType' in row and re.search(r'yes\s+yes\s+yes', row) for row in rows):
            raise AssertionError('Expected two embedded Unicode subset font resources')
        text = run('pdftotext', '-raw', pdf, '-').decode('utf-8'); extracted.append(text)
        if ''.join(text.split()) != expected[-1]: raise AssertionError('Visible HTML and PDF text differ: ' + repr(text))
        root = ET.fromstring(run('pdftotext', '-bbox', pdf, '-'))
        for page in root.iter('{http://www.w3.org/1999/xhtml}page'):
            for word in page.iter('{http://www.w3.org/1999/xhtml}word'):
                if not (0 <= float(word.attrib['xMin']) <= float(word.attrib['xMax']) <= float(page.attrib['width'])
                        and 0 <= float(word.attrib['yMin']) <= float(word.attrib['yMax']) <= float(page.attrib['height'])):
                    raise AssertionError('Text extends outside page')
        run('pdftoppm', '-r', '120', pdf, pdf.with_suffix(''))
    if expected[0] != expected[1] or extracted[0] != extracted[1]: raise AssertionError('Text differs between authored variants')
    if pdfs[0].read_bytes() != pdfs[1].read_bytes(): raise AssertionError('Stylesheet and inline oracle PDF bytes differ')
    for page in (1, 2):
        a = output / f'stylesheet-document-{page}.ppm'; b = output / f'inline-oracle-{page}.ppm'
        if a.read_bytes() != b.read_bytes(): raise AssertionError('Rendered pixels differ')
    run('pdftoppm', '-r', '120', '-png', pdfs[0], pdfs[0].with_suffix(''))
    # Exercise the newly accepted colour-only shorthand using the real compiled Native AOT CLI.
    # Intermediate PDFs remain local temporary files, never part of the public evidence artifact.
    with tempfile.TemporaryDirectory(prefix='factspdf-background-') as tmp:
        temp = Path(tmp)
        short = temp / 'short.html'; long = temp / 'long.html'
        short.write_text("<div style='background:#cee8fb;padding:6pt'><p>Business report</p></div>", encoding='utf-8')
        long.write_text("<div style='background-color:#cee8fb;padding:6pt'><p>Business report</p></div>", encoding='utf-8')
        short_pdf = temp / 'short.pdf'; long_pdf = temp / 'long.pdf'
        run(exe, short, short_pdf)
        run(exe, long, long_pdf)
        if short_pdf.read_bytes() != long_pdf.read_bytes():
            raise AssertionError('Native AOT background shorthand PDF differs from background-color')
        run('qpdf', '--check', short_pdf)
        if 'Business report' not in run('pdftotext', '-raw', short_pdf, '-').decode('utf-8'):
            raise AssertionError('Native AOT shorthand report text was lost')
        bad = temp / 'bad.html'
        bad.write_text("<div style='background:url(secret.png)'>Not rendered</div>", encoding='utf-8')
        short_pdf.write_bytes(b'keep-existing')
        rejected = subprocess.run([str(exe), str(bad), str(short_pdf), '--overwrite'],
                                  capture_output=True, timeout=120)
        if rejected.returncode != 3 or b'FPDF1202' not in rejected.stderr or rejected.stdout:
            raise AssertionError('Unsupported background syntax was not rejected cleanly')
        if short_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Rejected background syntax modified existing output')
    # Actual Linux Native AOT: a customer-facing fontless print-unit report,
    # independent point-value oracle and invalid-margin destination recovery.
    with tempfile.TemporaryDirectory(prefix='factspdf-report-print-') as folder:
        temp = Path(folder)
        sample_pdf = temp / 'report.pdf'
        run(exe, Path('examples/report-print-spacing.html'), sample_pdf)
        run('qpdf', '--check', sample_pdf)
        info = run('pdfinfo', sample_pdf).decode('utf-8')
        if not re.search(r'^Pages:\s+1$', info, re.M):
            raise AssertionError('Print spacing expected one A4 page: ' + info)
        content = ' '.join(run('pdftotext', '-raw', sample_pdf, '-').decode('utf-8').split())
        for line in ('Service & Maintenance Report', 'Example Facilities Pty Ltd',
                     'Equipment seals and safety checks', 'Two items require follow-up'):
            if line not in content:
                raise AssertionError('Native sample missing text: ' + line)

        # Verify the actual Native AOT report's bounded nowrap path.
        # A fitting no-wrap label paints the same bytes as normal, while
        # an overwide label must reject rather than wrap, clip or overwrite.
        source_html = Path('examples/report-print-spacing.html').read_text(encoding='utf-8')
        if 'white-space: nowrap;' not in source_html:
            raise AssertionError('Print report is missing the nowrap reference case')
        normal_html = temp / 'report-normal-whitespace.html'
        normal_html.write_text(source_html.replace('white-space: nowrap;', 'white-space: normal;'),
                               encoding='utf-8')
        normal_pdf = temp / 'report-normal-whitespace.pdf'
        run(exe, normal_html, normal_pdf)
        run('qpdf', '--check', normal_pdf)
        if sample_pdf.read_bytes() != normal_pdf.read_bytes():
            raise AssertionError('A fitting nowrap report label changed normal PDF paint')
        if 'INV-2026-1042' not in content:
            raise AssertionError('Native AOT print report lost the no-wrap reference')
        narrow_normal = temp / 'narrow-normal.html'
        narrow_normal.write_text("<p style='width:72pt;white-space:normal'>Item paid in full</p>",
                                 encoding='utf-8')
        narrow_pdf = temp / 'narrow-normal.pdf'
        run(exe, narrow_normal, narrow_pdf)
        run('qpdf', '--check', narrow_pdf)
        if 'Item paid in full' not in ' '.join(
                run('pdftotext', '-raw', narrow_pdf, '-').decode('utf-8').split()):
            raise AssertionError('Normal narrow paragraph failed to wrap its words')
        narrow_nowrap = temp / 'narrow-nowrap.html'
        narrow_nowrap.write_text("<p style='width:72pt;white-space:nowrap'>Item paid in full</p>",
                                 encoding='utf-8')
        narrow_pdf.write_bytes(b'retained-report')
        failed = subprocess.run([str(exe), str(narrow_nowrap), str(narrow_pdf), '--overwrite'],
                                capture_output=True, timeout=120)
        if failed.returncode != 3 or b'FPDF1302' not in failed.stderr or failed.stdout:
            raise AssertionError('Native AOT nowrap did not reject a truly overwide label')
        if narrow_pdf.read_bytes() != b'retained-report':
            raise AssertionError('Overwide nowrap replaced an existing report')

        # This actual commercial report must render a first-line inset. An
        # independently authored 18pt projection must be byte-identical to
        # the 0.25in source; removing the inset must alter PDF geometry while
        # preserving the complete searchable report text.
        if 'text-indent: 0.25in;' not in source_html:
            raise AssertionError('Print report lacks a text-indent first-line case')
        point_html = temp / 'report-indent-points.html'
        zero_html = temp / 'report-indent-zero.html'
        point_html.write_text(source_html.replace('text-indent: 0.25in;', 'text-indent: 18pt;'), encoding='utf-8')
        zero_html.write_text(source_html.replace('text-indent: 0.25in;', 'text-indent: 0;'), encoding='utf-8')
        point_pdf, zero_pdf = temp / 'report-indent-points.pdf', temp / 'report-indent-zero.pdf'
        run(exe, point_html, point_pdf)
        run(exe, zero_html, zero_pdf)
        run('qpdf', '--check', point_pdf)
        if point_pdf.read_bytes() != sample_pdf.read_bytes():
            raise AssertionError('Native physical first-line indent differs from equivalent points')
        if zero_pdf.read_bytes() == sample_pdf.read_bytes():
            raise AssertionError('Native first-line indentation did not change report PDF geometry')
        if ' '.join(run('pdftotext', '-raw', zero_pdf, '-').decode('utf-8').split()) != content:
            raise AssertionError('Native first-line indentation lost or altered report text')
        indent_bad = temp / 'invalid-indent.html'
        indent_recovery = temp / 'indent-recovery.pdf'
        indent_bad.write_text("<p style='width:40pt;text-indent:35pt'>LONG</p>", encoding='utf-8')
        indent_recovery.write_bytes(b'keep-original')
        indent_failure = subprocess.run([str(exe), str(indent_bad), str(indent_recovery), '--overwrite'],
                                        capture_output=True, timeout=120)
        if indent_failure.returncode != 3 or b'FPDF1302' not in indent_failure.stderr or indent_failure.stdout:
            raise AssertionError('Native overindented first word was not rejected cleanly')
        if indent_recovery.read_bytes() != b'keep-original':
            raise AssertionError('Native text-indent failure overwrote the destination')

        # Letter spacing is a *measured glyph advance* in the real report,
        # not merely accepted syntax: physical units must match a separately
        # authored point projection, with a different zero-spacing PDF.
        if 'letter-spacing: 0.01in;' not in source_html:
            raise AssertionError('Print report lacks tracked invoice reference')
        tracking_pt = temp / 'tracked-points.html'
        tracking_zero = temp / 'tracked-zero.html'
        tracking_pt.write_text(source_html.replace('letter-spacing: 0.01in;', 'letter-spacing: 0.72pt;'),
                               encoding='utf-8')
        tracking_zero.write_text(source_html.replace('letter-spacing: 0.01in;', 'letter-spacing: normal;'),
                                 encoding='utf-8')
        tracked_pt_pdf = temp / 'tracked-points.pdf'
        tracked_zero_pdf = temp / 'tracked-zero.pdf'
        run(exe, tracking_pt, tracked_pt_pdf)
        run(exe, tracking_zero, tracked_zero_pdf)
        run('qpdf', '--check', tracked_pt_pdf)
        if tracked_pt_pdf.read_bytes() != sample_pdf.read_bytes():
            raise AssertionError('Native tracking inches differ from equivalent points')
        if tracked_zero_pdf.read_bytes() == sample_pdf.read_bytes():
            raise AssertionError('Native letter spacing did not alter PDF positioning')
        tracked_qdf = temp / 'tracked-readable.pdf'
        run('qpdf', '--qdf', '--object-streams=disable', sample_pdf, tracked_qdf)
        if b'0.72 Tc' not in tracked_qdf.read_bytes():
            raise AssertionError('Native PDF lacks the measured 0.72pt character-spacing command')
        tracking_bad = temp / 'tracking-bad.html'
        tracking_bad.write_text("<p style='letter-spacing:-1pt'>Protected report</p>", encoding='utf-8')
        tracked_pt_pdf.write_bytes(b'keep-existing')
        failure = subprocess.run([str(exe), str(tracking_bad), str(tracked_pt_pdf), '--overwrite'],
                                 capture_output=True, timeout=120)
        if failure.returncode != 3 or b'FPDF1202' not in failure.stderr or failure.stdout:
            raise AssertionError('Native invalid letter-spacing was not rejected')
        if tracked_pt_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Native invalid letter-spacing modified the original file')

        # Real Native AOT word spacing: physical units match independently
        # authored points, painted TJ changes PDF, and invalid CSS preserves
        # existing target bytes instead of silently dropping the declaration.
        if 'word-spacing: 0.01in;' not in source_html:
            raise AssertionError('Report lacks word-spaced explanatory prose')
        words_pt = temp / 'word-gap-points.html'
        words_normal = temp / 'word-gap-normal.html'
        words_pt.write_text(source_html.replace('word-spacing: 0.01in;', 'word-spacing: 0.72pt;'),
                            encoding='utf-8')
        words_normal.write_text(source_html.replace('word-spacing: 0.01in;', 'word-spacing: normal;'),
                                encoding='utf-8')
        words_pt_pdf, words_normal_pdf = temp / 'word-gap-points.pdf', temp / 'word-gap-normal.pdf'
        run(exe, words_pt, words_pt_pdf)
        run(exe, words_normal, words_normal_pdf)
        run('qpdf', '--check', words_pt_pdf)
        if words_pt_pdf.read_bytes() != sample_pdf.read_bytes():
            raise AssertionError('Native physical word spacing differs from equivalent points')
        if words_normal_pdf.read_bytes() == sample_pdf.read_bytes():
            raise AssertionError('Native word spacing did not change PDF paint')
        if 'Service detail:' not in ' '.join(run('pdftotext', '-raw', words_pt_pdf, '-').decode().split()):
            raise AssertionError('Native word spacing corrupted searchable report text')
        word_qdf = temp / 'word-gap-readable.pdf'
        run('qpdf', '--qdf', '--object-streams=disable', sample_pdf, word_qdf)
        if b'] TJ' not in word_qdf.read_bytes():
            raise AssertionError('Native report does not contain PDF TJ word displacement')
        bad_words = temp / 'word-gap-invalid.html'
        bad_words.write_text("<p style='word-spacing:-1pt'>Keep the original report</p>",
                             encoding='utf-8')
        words_pt_pdf.write_bytes(b'keep-existing')
        word_failure = subprocess.run([str(exe), str(bad_words), str(words_pt_pdf), '--overwrite'],
                                      capture_output=True, timeout=120)
        if word_failure.returncode != 3 or b'FPDF1202' not in word_failure.stderr or word_failure.stdout:
            raise AssertionError('Native AOT accepted invalid word spacing')
        if words_pt_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Native invalid word spacing modified original PDF')

        # Independent Native AOT fixture: added TJ word displacements occur
        # only when a line wraps naturally, and report text stays searchable.
        if 'text-align: justify;' not in source_html:
            raise AssertionError('Print report lacks a justified paragraph')
        report_left = temp / 'report-left-aligned.html'
        report_left.write_text(source_html.replace('text-align: justify;', 'text-align: left;'),
                               encoding='utf-8')
        report_left_pdf = temp / 'report-left-aligned.pdf'
        run(exe, report_left, report_left_pdf)
        run('qpdf', '--check', report_left_pdf)
        if report_left_pdf.read_bytes() == sample_pdf.read_bytes():
            raise AssertionError('Native justified report prose did not alter PDF geometry')
        if ' '.join(run('pdftotext', '-raw', report_left_pdf, '-').decode().split()) != content:
            raise AssertionError('Justification lost or altered business report text')
        words = 'One two three four five six seven eight nine ten eleven'
        source_justify = temp / 'standalone-justify.html'
        source_left = temp / 'standalone-left.html'
        source_justify.write_text("<p style='width:116pt;text-align:justify'>" + words + "</p>",
                                  encoding='utf-8')
        source_left.write_text("<p style='width:116pt;text-align:left'>" + words + "</p>",
                               encoding='utf-8')
        justify_pdf, left_pdf = temp / 'standalone-justify.pdf', temp / 'standalone-left.pdf'
        run(exe, source_justify, justify_pdf)
        run(exe, source_left, left_pdf)
        run('qpdf', '--check', justify_pdf)
        if justify_pdf.read_bytes() == left_pdf.read_bytes():
            raise AssertionError('Soft-wrap justification missing from Native AOT PDF')
        for source, qdf_target in ((justify_pdf, temp / 'justify-qdf.pdf'),
                                   (left_pdf, temp / 'left-qdf.pdf')):
            run('qpdf', '--qdf', '--object-streams=disable', source, qdf_target)
        if b'] TJ' not in (temp / 'justify-qdf.pdf').read_bytes():
            raise AssertionError('Native justified word spaces lack PDF TJ')
        if b'] TJ' in (temp / 'left-qdf.pdf').read_bytes():
            raise AssertionError('Left-aligned fixture unexpectedly uses TJ')
        if ' '.join(run('pdftotext', '-raw', justify_pdf, '-').decode().split()) != ' '.join(
                run('pdftotext', '-raw', left_pdf, '-').decode().split()):
            raise AssertionError('Native justification changed searchable words')
        invalid = temp / 'invalid-justify.html'
        invalid.write_text("<p style='text-align:justify-all'>Protected report</p>",
                           encoding='utf-8')
        left_pdf.write_bytes(b'keep-existing')
        rejected = subprocess.run([str(exe), str(invalid), str(left_pdf), '--overwrite'],
                                  capture_output=True, timeout=120)
        if rejected.returncode != 3 or b'FPDF1202' not in rejected.stderr or rejected.stdout:
            raise AssertionError('Native invalid justify value was accepted')
        if left_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Rejected justify value overwrote the original PDF')

        # The real Native AOT binary must paint, not just accept, the two
        # current-source text decorations in the runnable business sample.
        for label in ('Previous estimate', 'Revised estimate'):
            if label not in content:
                raise AssertionError('Native decorated report lost ' + label)
        qdf = temp / 'report-readable.pdf'
        run('qpdf', '--qdf', '--object-streams=disable', sample_pdf, qdf)
        # Both 11pt paragraph decorations are 0.66pt solid fills; painted
        # borders/panels use other thicknesses. qdf uncompresses page streams.
        lines = re.findall(rb'\b[0-9]+(?:\.[0-9]+)? 0\.66 re f Q\n', qdf.read_bytes())
        if len(lines) != 2:
            raise AssertionError(f'Expected two actual Native AOT decoration paths, found {len(lines)}')
        short = temp / 'short.html'
        long = temp / 'long.html'
        short.write_text("<div style='width:2in;padding:0.125in;border:0.03125in solid red'>"
                         "<p style='font-size:0.25in;margin:0.125in'>Native report</p></div>",
                         encoding='utf-8')
        long.write_text("<div style='width:144pt;padding:9pt;border:2.25pt solid red'>"
                        "<p style='font-size:18pt;margin-top:9pt;margin-right:9pt;margin-bottom:9pt;margin-left:9pt'>Native report</p></div>",
                        encoding='utf-8')
        short_pdf, long_pdf = temp / 'short.pdf', temp / 'long.pdf'
        run(exe, short, short_pdf)
        run(exe, long, long_pdf)
        if short_pdf.read_bytes() != long_pdf.read_bytes():
            raise AssertionError('Native print units and four-sided margin differ from explicit point longhands')
        run('qpdf', '--check', short_pdf)

        bad = temp / 'invalid.html'
        bad.write_text("<p style='margin:-1mm'>Invalid negative margin</p>", encoding='utf-8')
        sample_pdf.write_bytes(b'keep-existing')
        proc = subprocess.run([str(exe), str(bad), str(sample_pdf), '--overwrite'],
                              capture_output=True, timeout=120)
        if proc.returncode != 3 or b'FPDF1202' not in proc.stderr or proc.stdout:
            raise AssertionError('Native CLI did not strictly reject invalid negative margin')
        if sample_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Invalid native margin changed existing output')
        # Compare the real, printable customer template against a separate
        # opaque-hex projection. This tests all paint sites with the compiled
        # Linux Native AOT executable, not just a CSS parser acceptance.
        source = Path('examples/report-print-spacing.html').read_text(encoding='utf-8')
        substitutions = {
            'rgb(37, 48, 71)': '#253047',
            'rgb(35, 68, 107)': '#23446b',
            'rgb(35 68 107)': '#23446b',
            'rgb(240 246 250)': '#f0f6fa',
            'rgba(219, 234, 240, 1)': '#dbeaf0',
        }
        for old, new in substitutions.items():
            if old not in source:
                raise AssertionError('RGB report fixture did not contain ' + old)
            source = source.replace(old, new)
        hex_source = temp / 'report-hex.html'
        hex_source.write_text(source, encoding='utf-8')
        hex_pdf = temp / 'report-hex.pdf'
        run(exe, hex_source, hex_pdf)
        run('qpdf', '--check', hex_pdf)
        # The report path was already exercised before destination recovery.
        rgb_pdf = temp / 'report-rgb.pdf'
        run(exe, Path('examples/report-print-spacing.html'), rgb_pdf)
        if rgb_pdf.read_bytes() != hex_pdf.read_bytes():
            raise AssertionError('Native AOT opaque RGB function paint differs from hex')
        # A second independently authored projection proves real
        # parent-relative typography, not just CSS parse acceptance.
        relative_source = Path('examples/report-print-spacing.html').read_text(encoding='utf-8')
        absolute_typography = {
            'font-size: 125%': 'font-size: 13.75pt',
            'font-size: 1.125em': 'font-size: 12.375pt',
        }
        for relative, absolute in absolute_typography.items():
            if relative not in relative_source:
                raise AssertionError('Relative typography fixture did not contain ' + relative)
            relative_source = relative_source.replace(relative, absolute)
        absolute_html = temp / 'report-absolute-font.html'
        absolute_html.write_text(relative_source, encoding='utf-8')
        absolute_pdf = temp / 'report-absolute-font.pdf'
        run(exe, absolute_html, absolute_pdf)
        run('qpdf', '--check', absolute_pdf)
        if rgb_pdf.read_bytes() != absolute_pdf.read_bytes():
            raise AssertionError('Native AOT relative font-size paint differs from resolved pt sizes')
        # Independently authored resolved-point line heights must create
        # identical real Native AOT output to percentage, em and inch inputs.
        # Each target remains local; the test must inspect actual CLI PDFs.
        line_source = Path('examples/report-print-spacing.html').read_text(encoding='utf-8')
        line_projection = {
            'line-height: 160%': 'line-height: 22pt',
            'line-height: 1.5em': 'line-height: 16.5pt',
            'line-height: 0.25in': 'line-height: 18pt',
        }
        for relative, absolute in line_projection.items():
            if relative not in line_source:
                raise AssertionError('Print report does not exercise ' + relative)
            line_source = line_source.replace(relative, absolute)
        line_html = temp / 'report-absolute-leading.html'
        line_html.write_text(line_source, encoding='utf-8')
        line_pdf = temp / 'report-absolute-leading.pdf'
        run(exe, line_html, line_pdf)
        run('qpdf', '--check', line_pdf)
        if rgb_pdf.read_bytes() != line_pdf.read_bytes():
            raise AssertionError('Native AOT computed line-height does not match actual pt line boxes')
        bad.write_text("<p style='line-height:0%'>Invalid zero line height</p>",
                       encoding='utf-8')
        proc = subprocess.run([str(exe), str(bad), str(sample_pdf), '--overwrite'],
                              capture_output=True, timeout=120)
        if proc.returncode != 3 or b'FPDF1202' not in proc.stderr or proc.stdout:
            raise AssertionError('Native AOT accepted invalid percent line-height')
        if sample_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Rejected line height modified existing PDF')
        bad.write_text("<p style='font-size:200em'>Invalid relative font size</p>",
                       encoding='utf-8')
        proc = subprocess.run([str(exe), str(bad), str(sample_pdf), '--overwrite'],
                              capture_output=True, timeout=120)
        if proc.returncode != 3 or b'FPDF1202' not in proc.stderr or proc.stdout:
            raise AssertionError('Native AOT accepted out-of-range relative font size')
        if sample_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Invalid relative font size modified the destination')
        bad.write_text("<div style='background:rgba(1,2,3,0.5)'>Invalid alpha</div>", encoding='utf-8')
        proc = subprocess.run([str(exe), str(bad), str(sample_pdf), '--overwrite'],
                              capture_output=True, timeout=120)
        if proc.returncode != 3 or b'FPDF1202' not in proc.stderr or proc.stdout:
            raise AssertionError('Native AOT accepted unsupported alpha channel')
        if sample_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Unsupported RGB alpha modified the destination')
        # A rejected decoration cannot corrupt a previous PDF, even in AOT.
        bad.write_text("<p style='text-decoration:overline'>Unsupported</p>", encoding='utf-8')
        proc = subprocess.run([str(exe), str(bad), str(sample_pdf), '--overwrite'],
                              capture_output=True, timeout=120)
        if proc.returncode != 3 or b'FPDF1202' not in proc.stderr or proc.stdout:
            raise AssertionError('Native unsupported decoration did not fail cleanly')
        if sample_pdf.read_bytes() != b'keep-existing':
            raise AssertionError('Native unsupported decoration mutated the destination')
        # Verify that an authored border-box width changes actual native PDF
        # geometry, by comparing with an independently specified content width.
        border_html = temp / 'border-box.html'
        content_html = temp / 'content-box.html'
        border_html.write_text(
            "<div style='box-sizing:border-box;width:160pt;padding:10pt;"
            "border:2pt solid blue;background:#f0f6fa'>BORDER BOX</div>",
            encoding='utf-8')
        content_html.write_text(
            "<div style='width:136pt;padding:10pt;"
            "border:2pt solid blue;background:#f0f6fa'>BORDER BOX</div>",
            encoding='utf-8')
        native_border = temp / 'border-box.pdf'
        native_content = temp / 'content-box.pdf'
        run(exe, border_html, native_border)
        run(exe, content_html, native_content)
        run('qpdf', '--check', native_border)
        if native_border.read_bytes() != native_content.read_bytes():
            raise AssertionError('Native border-box/content-box PDF equivalence failed')
        if 'BORDER BOX' not in run('pdftotext', '-raw', native_border, '-').decode('utf-8'):
            raise AssertionError('Native border-box text is not searchable')
        recovery = temp / 'border-box-recovery.pdf'
        for css, code in (
            ("box-sizing:padding-box", b'FPDF1202'),
            ("box-sizing:border-box;width:10pt;padding:6pt", b'FPDF1302')
        ):
            bad.write_text("<div style='" + css + "'>Invalid</div>", encoding='utf-8')
            recovery.write_bytes(b'keep-existing')
            rejected = subprocess.run([str(exe), str(bad), str(recovery), '--overwrite'],
                                      capture_output=True, timeout=120)
            if rejected.returncode != 3 or code not in rejected.stderr or rejected.stdout:
                raise AssertionError('Native border-box rejection failed for ' + css)
            if recovery.read_bytes() != b'keep-existing':
                raise AssertionError('Native border-box failure modified existing output')
        print('Native print spacing, tracked reference, word gaps, first-line indentation, border-box, computed line-height, relative font sizing, RGB colour and decoration PDF paths, qpdf, text, recovery verified')

    # Real source-built Linux Native AOT: casing must change emitted glyphs,
    # not merely CSS parsing or a display-time metadata field. The oracle is
    # hand-authored visible text with no transformation declarations.
    with tempfile.TemporaryDirectory(prefix='factspdf-case-labels-') as folder:
        temp = Path(folder)
        actual, expected = temp / 'case-labels.pdf', temp / 'case-oracle.pdf'
        run(exe, Path('examples/report-case-labels.html'), actual)
        run(exe, Path('examples/report-case-labels-oracle.html'), expected)
        run('qpdf', '--check', actual)
        if actual.read_bytes() != expected.read_bytes():
            raise AssertionError('Native ASCII transformed glyphs differ from authored-case PDF oracle')
        extracted = ' '.join(run('pdftotext', '-raw', actual, '-').decode('utf-8').split())
        for phrase in ('SERVICE STATUS REPORT', 'REFERENCE: INV-2026-1042',
                       'status: pending review', 'ASSIGNED TO Example Facilities Pty Ltd'):
            if phrase not in extracted:
                raise AssertionError('Native case report lost expected searchable content: ' + phrase)
        destination = temp / 'preserved.pdf'
        invalid = temp / 'invalid.html'
        for content, code in (
            ("<p style='text-transform:capitalize'>Wrong form</p>", b'FPDF1202'),
            ("<p style='text-transform:uppercase'>caf\\u00e9</p>".replace('\\u00e9', '\u00e9'), b'FPDF1305')
        ):
            invalid.write_text(content, encoding='utf-8')
            destination.write_bytes(b'keep-existing')
            failed = subprocess.run([str(exe), str(invalid), str(destination), '--overwrite'],
                                    capture_output=True, timeout=120)
            if failed.returncode != 3 or code not in failed.stderr or failed.stdout:
                raise AssertionError('Native case-transform rejection did not return ' + str(code))
            if destination.read_bytes() != b'keep-existing':
                raise AssertionError('Rejected native case-transform changed existing report')

    # Executable width-constraint report and independently authored fixed-width
    # projection: real PDF bytes, searchable text and protected-target failures.
    with tempfile.TemporaryDirectory(prefix='factspdf-width-constraints-') as folder:
        temp = Path(folder)
        actual, expected = temp / 'constrained.pdf', temp / 'fixed-width.pdf'
        run(exe, Path('examples/report-width-constraints.html'), actual)
        run(exe, Path('examples/report-width-constraints-oracle.html'), expected)
        run('qpdf', '--check', actual)
        run('qpdf', '--check', expected)
        if actual.read_bytes() != expected.read_bytes():
            raise AssertionError('Native min/max-width PDF layout differs from explicit-width projection')
        searchable = ' '.join(run('pdftotext', '-raw', actual, '-').decode('utf-8').split())
        for phrase in ('Service Report', 'Example Facilities Pty Ltd', 'SR-2026-0142', 'Outcome: Site inspection completed.'):
            if phrase not in searchable:
                raise AssertionError('Width-constrained report lost searchable text: ' + phrase)
        bad, destination = temp / 'invalid.html', temp / 'preserved.pdf'
        for invalid_css, code in (
            ('max-width:auto', b'FPDF1202'),
            ('min-width:300mm', b'FPDF1302'),
            ('box-sizing:border-box;max-width:2pt;padding:10pt', b'FPDF1302')
        ):
            bad.write_text("<p style='" + invalid_css + "'>Not generated</p>", encoding='utf-8')
            destination.write_bytes(b'keep-existing')
            failed = subprocess.run([str(exe), str(bad), str(destination), '--overwrite'],
                                    capture_output=True, timeout=120)
            if failed.returncode != 3 or code not in failed.stderr or failed.stdout:
                raise AssertionError('Native width-constraint rejection failed: ' + invalid_css)
            if destination.read_bytes() != b'keep-existing':
                raise AssertionError('Native invalid width constraint replaced an existing report')

    # Real native CLI: root-relative font and line boxes compared with
    # independent absolute point values on a paginated business report.
    with tempfile.TemporaryDirectory(prefix='factspdf-rem-typography-') as folder:
        temp = Path(folder)
        actual, expected = temp / 'rem.pdf', temp / 'points.pdf'
        run(exe, Path('examples/report-root-relative-typography.html'), actual)
        run(exe, Path('examples/report-root-relative-typography-oracle.html'), expected)
        run('qpdf', '--check', actual)
        run('qpdf', '--check', expected)
        if actual.read_bytes() != expected.read_bytes():
            raise AssertionError('Native rem PDF differs from point-size projection')
        info = run('pdfinfo', actual).decode('utf-8')
        page_line = next((line for line in info.splitlines() if line.startswith('Pages:')), None)
        if page_line is None or int(page_line.split(':', 1)[1].strip()) < 2:
            raise AssertionError('Native rem report did not paginate: ' + info)
        visible = ' '.join(run('pdftotext', '-raw', actual, '-').decode('utf-8').split())
        for phrase in ('Business Service Report', 'Example Facilities Pty Ltd',
                       'AB-WA-1042', 'Service record'):
            if phrase not in visible:
                raise AssertionError('Native rem PDF lost searchable text: ' + phrase)
        bad, destination = temp / 'invalid.html', temp / 'preserved.pdf'
        for declaration in ('font-size:0rem', 'font-size:1000rem',
                            'line-height:0.001rem', 'line-height:2000rem', 'width:1rem'):
            bad.write_text("<html style='font-size:15pt'><body><p style='" +
                           declaration + "'>Invalid</p></body></html>", encoding='utf-8')
            destination.write_bytes(b'keep-existing')
            failed = subprocess.run([str(exe), str(bad), str(destination), '--overwrite'],
                                    capture_output=True, timeout=120)
            if failed.returncode != 3 or b'FPDF1202' not in failed.stderr or failed.stdout:
                raise AssertionError('Native rem negative input accepted: ' + declaration)
            if destination.read_bytes() != b'keep-existing':
                raise AssertionError('Native rem failure changed the existing target')
        print('Native root-relative PDF parity, pagination, searchable text and recovery verified')

    report = {
        'source_sha': os.environ.get('FACTSPDF_SOURCE_SHA'),
        'checkout_sha': run('git', 'rev-parse', 'HEAD').decode().strip(),
        'pages': 2, 'pdf_bytes': pdfs[0].stat().st_size,
        'pdf_sha256': hashlib.sha256(pdfs[0].read_bytes()).hexdigest(),
        'input_sha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},
        'font_inputs': [{'name': p.name, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in fonts],
        'checks': {'pdf_bytes_equal': True, 'extracted_text_equal': True, 'text_matches_html': True,
                   'page_pixels_equal_120dpi': True, 'text_inside_page': True, 'two_embedded_subset_fonts': True, 'native_color_background_parity': True,
                   'native_background_recovery': True, 'native_print_spacing_and_recovery': True,
                    'native_text_decoration_paths_and_recovery': True,
                     'native_opaque_rgb_equivalence_and_recovery': True,
                     'native_relative_font_sizing_equivalence_and_recovery': True,
                     'native_computed_line_height_equivalence_and_recovery': True,
                      'native_border_box_equivalence_and_recovery': True,
                      'native_text_indent_geometry_and_recovery': True,
                      'native_letter_spacing_geometry_and_recovery': True,
                       'native_word_spacing_geometry_and_recovery': True,
                       'native_report_justification_and_recovery': True,
                       'native_ascii_case_glyph_parity_and_recovery': True,
                       'native_min_max_width_pdf_equivalence_and_recovery': True,
                       'native_root_relative_typography_equivalence_and_recovery': True},
        'scope': 'One bilingual controlled fixture plus NUnit regressions. This is not browser/full-CSS conformance, a benchmark or independent code review.'
    }
    (output / 'verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__': main()
