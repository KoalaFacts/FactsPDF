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
    # Execute the customer-visible fontless business example on the actual Linux
    # Native AOT product and compare a separate hand-authored print-unit oracle.
    # These temporary outputs are not publicly distributed.
    with tempfile.TemporaryDirectory(prefix='factspdf-report-spacing-') as tmp:
        temp = Path(tmp)
        sample = Path('examples/report-print-spacing.html')
        actual_pdf = temp / 'print-spacing.pdf'
        run(exe, sample, actual_pdf)
        run('qpdf', '--check', actual_pdf)
        info = run('pdfinfo', actual_pdf).decode('utf-8')
        if not re.search(r'^Pages:\s+1        'source_sha': os.environ.get('FACTSPDF_SOURCE_SHA'),
        'checkout_sha': run('git', 'rev-parse', 'HEAD').decode().strip(),
        'pages': 2, 'pdf_bytes': pdfs[0].stat().st_size,
        'pdf_sha256': hashlib.sha256(pdfs[0].read_bytes()).hexdigest(),
        'input_sha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},
        'font_inputs': [{'name': p.name, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in fonts],
        'checks': {'pdf_bytes_equal': True, 'extracted_text_equal': True, 'text_matches_html': True,
                   'page_pixels_equal_120dpi': True, 'text_inside_page': True, 'two_embedded_subset_fonts': True, 'native_color_background_parity': True,
                   'native_background_recovery': True, 'native_print_spacing_and_recovery': True},
        'scope': 'One bilingual controlled fixture plus NUnit regressions. This is not browser/full-CSS conformance, a benchmark or independent code review.'
    }
    (output / 'verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__': main()
, info, re.M):
            raise AssertionError('Unexpected native print-spacing page count: ' + info)
        content = run('pdftotext', '-raw', actual_pdf, '-').decode('utf-8')
        for expected_text in ('Service & Maintenance Report', 'Example Facilities Pty Ltd',
                              'Equipment seals and safety checks', 'Two items require follow-up'):
            if expected_text not in content:
                raise AssertionError('Native print-spacing report lost ' + expected_text)
        # 1in = 72pt; 0.125in = 9pt; 0.03125in = 2.25pt;
        # 0.25in = 18pt. Render the same content, not a shared parser output.
        short_html = ("<div style='width:1in;padding:0.125in;border:0.03125in solid red'>"
                      "<p style='font-size:0.25in;margin:0 0 0.125in'>Native report</p></div>")
        long_html = ("<div style='width:72pt;padding:9pt;border:2.25pt solid red'>"
                     "<p style='font-size:18pt;margin-top:0;margin-bottom:9pt'>Native report</p></div>")
        short_path, long_path = temp / 'short.html', temp / 'long.html'
        short_path.write_text(short_html, encoding='utf-8')
        long_path.write_text(long_html, encoding='utf-8')
        short_pdf, long_pdf = temp / 'short.pdf', temp / 'long.pdf'
        run(exe, short_path, short_pdf)
        run(exe, long_path, long_pdf)
        if short_pdf.read_bytes() != long_pdf.read_bytes():
            raise AssertionError('Native AOT margin shorthand/print units differ from pt longhands')
        run('qpdf', '--check', short_pdf)
        bad = temp / 'invalid.html'
        bad.write_text("<p style='margin:5mm'>Must not silently discard horizontal margins</p>",
                       encoding='utf-8')
        actual_pdf.write_bytes(b'preserve-existing-file')
        invalid = subprocess.run([str(exe), str(bad), str(actual_pdf), '--overwrite'],
                                 capture_output=True, timeout=120)
        if invalid.returncode != 3 or b'FPDF1202' not in invalid.stderr or invalid.stdout:
            raise AssertionError('Native AOT silently accepted unsupported horizontal margin')
        if actual_pdf.read_bytes() != b'preserve-existing-file':
            raise AssertionError('Native AOT invalid print margin changed destination')
        print('Native AOT report print-spacing: real sample, text, qpdf, oracle bytes and recovery PASS')
    report = {
        'source_sha': os.environ.get('FACTSPDF_SOURCE_SHA'),
        'checkout_sha': run('git', 'rev-parse', 'HEAD').decode().strip(),
        'pages': 2, 'pdf_bytes': pdfs[0].stat().st_size,
        'pdf_sha256': hashlib.sha256(pdfs[0].read_bytes()).hexdigest(),
        'input_sha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},
        'font_inputs': [{'name': p.name, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in fonts],
        'checks': {'pdf_bytes_equal': True, 'extracted_text_equal': True, 'text_matches_html': True,
                   'page_pixels_equal_120dpi': True, 'text_inside_page': True, 'two_embedded_subset_fonts': True, 'native_color_background_parity': True,
                   'native_background_recovery': True},
        'scope': 'One bilingual controlled fixture plus NUnit regressions. This is not browser/full-CSS conformance, a benchmark or independent code review.'
    }
    (output / 'verification.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__': main()
