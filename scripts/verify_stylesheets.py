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
        print('Native print spacing, border-box, computed line-height, relative font sizing, RGB colour and decoration PDF paths, qpdf, text, recovery verified')
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
                      'native_border_box_equivalence_and_recovery': True},
        'scope': 'One bilingual controlled fixture plus NUnit regressions. This is not browser/full-CSS conformance, a benchmark or independent code review.'
    }
    (output / 'verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__': main()
