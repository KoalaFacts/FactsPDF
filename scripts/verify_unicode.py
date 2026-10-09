"""Independent CI verification only. fontTools/Poppler/qpdf are NOT engine dependencies.
No font files are committed, packaged, uploaded, or logged. The optional PDF log
contains only our small public synthetic document, never user data.
"""
import base64
import hashlib
from html.parser import HTMLParser
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

from fontTools import subset
from fontTools.ttLib import TTFont


class VisibleText(HTMLParser):
    def __init__(self):
        super().__init__(); self.parts = []; self.in_head = False
    def handle_starttag(self, tag, attrs):
        if tag == 'head': self.in_head = True
    def handle_endtag(self, tag):
        if tag == 'head': self.in_head = False
    def handle_data(self, data):
        if not self.in_head: self.parts.append(data)


def command(*args):
    result = subprocess.run([str(a) for a in args], check=True, capture_output=True)
    return result.stdout.decode('utf-8')


def verify(pdf, expected):
    print(command('qpdf', '--check', pdf))
    info = command('pdfinfo', pdf)
    if not re.search(r'^Pages:\s+2$', info, re.M): raise AssertionError(info)
    fonts = command('pdffonts', pdf); print(fonts)
    rows = fonts.splitlines()[2:]
    if len(rows) != 2 or not all('CID TrueType' in row and re.search(r'yes\s+no\s+yes', row) for row in rows):
        raise AssertionError('Expected two fully embedded Unicode TrueType fonts')
    text = command('pdftotext', '-raw', pdf, '-')
    if ''.join(text.split()) != ''.join(expected.split()):
        raise AssertionError('Extracted text differs from visible HTML: ' + repr(text))
    xml = command('pdftotext', '-bbox', pdf, '-')
    root = ET.fromstring(xml)
    for page in root.iter('{http://www.w3.org/1999/xhtml}page'):
        for word in page.iter('{http://www.w3.org/1999/xhtml}word'):
            for value, limit in [('xMax', float(page.attrib['width'])), ('yMax', float(page.attrib['height']))]:
                if float(word.attrib[value]) > limit: raise AssertionError('Text outside page')
            if float(word.attrib['xMin']) < 0 or float(word.attrib['yMin']) < 0: raise AssertionError('Negative text bounds')
    prefix = pdf.with_suffix('')
    command('pdftoppm', '-r', '96', '-png', pdf, prefix)
    for i in (1, 2):
        image = Path(str(prefix) + f'-{i}.png')
        if not image.is_file() or image.stat().st_size < 1000: raise AssertionError('Missing rendered page')
    print(f'Verified text, embedded fonts and two rendered pages: {pdf.name} ({pdf.stat().st_size} bytes)')


def main():
    executable = Path(sys.argv[1]).resolve()
    html = Path('examples/unicode-document.html')
    parsed = VisibleText(); parsed.feed(html.read_text(encoding='utf-8')); expected = ''.join(parsed.parts)
    codepoints = sorted(set(map(ord, expected)) | {32})
    originals = [Path('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'), Path('/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf')]
    artifacts = Path('artifacts'); artifacts.mkdir(exist_ok=True)
    prepared = []
    for i, path in enumerate(originals):
        with TTFont(path, recalcTimestamp=False) as font:
            print('Verification font:', path.name, 'sha256=' + hashlib.sha256(path.read_bytes()).hexdigest(), 'fsType=' + str(font['OS/2'].fsType))
            if font['OS/2'].fsType != 0: raise AssertionError('This fixture requires installable embedding permissions')
            options = subset.Options(); options.recalc_timestamp = False; options.layout_features = []; options.notdef_glyph = True
            options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
            sub = subset.Subsetter(options=options); sub.populate(unicodes=codepoints); sub.subset(font)
            destination = artifacts / f'verification-font-{i}.ttf'; font.save(destination); prepared.append(destination)
    for source_set, filename in [(originals, 'unicode-full-fonts.pdf'), (prepared, 'unicode-document.pdf')]:
        pdf = artifacts / filename
        args = [executable, html, pdf]
        for path in source_set: args += ['--font', path]
        command(*args); verify(pdf, expected)
    pdf = artifacts / 'unicode-document.pdf'
    data = pdf.read_bytes(); print('FACTSPDF_UNICODE_SHA256=' + hashlib.sha256(data).hexdigest())
    print('Test font preparation used fontTools outside the engine. FactsPDF fully embeds supplied fonts; engine subsetting is NOT claimed.')
    if '--emit-base64' in sys.argv:
        if len(data) > 100_000: raise AssertionError('Fixture too large for safe diagnostic logging')
        print('FACTSPDF_UNICODE_BASE64=' + base64.b64encode(data).decode('ascii'))


if __name__ == '__main__': main()
