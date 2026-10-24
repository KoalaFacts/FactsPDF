"""Exercise real Linux/Windows Native AOT feature paths with independent Poppler checks.

The binary must be built on its native runner. This is a technical gate, not
human page approval, a release, or source-to-binary attestation.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from png_fixtures import create as create_png_fixtures

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(*args, cwd=None, exit_code=0):
    process = subprocess.run([str(arg) for arg in args], cwd=cwd, capture_output=True, timeout=120)
    assert process.returncode == exit_code, (args, process.returncode,
                                             process.stderr.decode('utf-8', 'replace'))
    return process.stdout


def count_pages(pdf):
    info = run('pdfinfo', pdf).decode('utf-8')
    match = re.search(r'^Pages:\s+(\d+)\s*$', info, re.MULTILINE)
    assert match, 'Missing independent page count'
    return int(match.group(1))


def page_lines(pdf, page):
    raw = run('pdftotext', '-f', page, '-l', page, '-raw', pdf, '-').decode('utf-8')
    return [line.strip() for line in raw.splitlines() if line.strip()]


def page_boxes(pdf, page):
    raw = run('pdftotext', '-f', page, '-l', page, '-bbox', pdf, '-')
    document = ET.fromstring(raw)
    pages = [item for item in document.iter() if item.tag.rsplit('}', 1)[-1] == 'page']
    assert len(pages) == 1, 'Unexpected bbox page count'
    node = pages[0]
    width, height = float(node.attrib['width']), float(node.attrib['height'])
    words = [(item.text, float(item.attrib['xMin']), float(item.attrib['yMin']),
              float(item.attrib['xMax']), float(item.attrib['yMax'])) for item in node.iter()
             if item.tag.rsplit('}', 1)[-1] == 'word']
    return width, height, words


def require_region(words, token, width, height, side, band):
    matches = [word for word in words if word[0] == token]
    assert len(matches) == 1, ('Missing or duplicate furniture token', token, matches)
    _, left, top, right, bottom = matches[0]
    assert (right < width / 2 if side == 'left' else left > width / 2), ('Wrong horizontal region', token)
    assert (bottom < 50 if band == 'header' else top > height - 50), ('Wrong page-margin region', token)
    assert 2 <= left < right <= width - 2 and 2 <= top < bottom <= height - 2, (
        'Outside page clearance', token)


def validate_furniture_words(original, decorated, page):
    """Independent body-geometry control: furniture only adds expected margin ink."""
    width, height, baseline = original
    other_width, other_height, written = decorated
    assert (width, height) == (other_width, other_height), 'Page geometry changed'
    extra = Counter(written) - Counter(baseline)
    missing = Counter(baseline) - Counter(written)
    assert not missing, ('Body word/position lost or moved', page, missing)
    tokens = ['Page', str(page), 'of', '2']
    if page == 2:
        tokens += ['Business', 'report', 'Internal', 'evaluation', 'Prepared', 'offline']
    assert Counter(word[0] for word in extra.elements()) == Counter(tokens), (
        'Unexpected furniture words or duplicates', page, list(extra.elements()))
    assert sum(extra.values()) == len(tokens), 'Unexpected additional PDF text'
    if page == 2:
        require_region(written, 'Business', width, height, 'left', 'header')
        require_region(written, 'Internal', width, height, 'right', 'header')
        require_region(written, 'Prepared', width, height, 'left', 'footer')
    require_region(written, 'Page', width, height, 'right', 'footer')
    assert all(item[4] < 50 or item[2] > height - 50 for item in extra.elements()), (
        'Added text outside page margins', page)
    return dict(page=page, addedWords=len(tokens), bodyWordsUnchanged=len(baseline))


def validate_continued_row(pages):
    assert len(pages) == 2
    text = '\n'.join('\n'.join(page) for page in pages)
    numbers = re.findall(r'(?<!\d)(?:0[1-9]|1[0-9]|2[0-4])\.(?=\s)', text)
    expected = [f'{number:02d}.' for number in range(1, 25)]
    assert numbers == expected, ('Continued note missing, repeated or out of order', numbers)
    for marker in ('SRV-001', 'SRV-002', '600.00', '300.00', '900.00', 'TOTAL / ILLUSTRATIVE'):
        assert text.count(marker) == 1, ('Missing/duplicate value', marker, text.count(marker))
    header = 'Description / delivery notes'
    assert text.count(header) == 2, ('Repeated table heading lost', text.count(header))
    assert re.findall(r'(?<!\d)(?:0[1-9]|1[0-9]|2[0-4])\.(?=\s)', '\n'.join(pages[0])) == expected[:12], 'First page notes'
    assert re.findall(r'(?<!\d)(?:0[1-9]|1[0-9]|2[0-4])\.(?=\s)', '\n'.join(pages[1])) == expected[12:], 'Second page notes'
    return dict(notes=24, pages=2, repeatedHeaders=2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--executable', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    original = args.executable.resolve()
    assert original.is_file() and original.name in ('FactsPDF.Cli', 'FactsPDF.Cli.exe'), 'Expected native executable'
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    first = ROOT / 'examples/first-document.html'
    long = ROOT / 'examples/business-table-long-description.html'
    config = ROOT / 'examples/business-page-furniture.json'
    assert all(path.is_file() for path in (first, long, config)), 'Missing shipped fixture'
    with tempfile.TemporaryDirectory(prefix='factspdf-native-features-') as directory:
        work = Path(directory)
        isolated = work / original.name
        shutil.copyfile(original, isolated)
        if original.suffix != '.exe':
            isolated.chmod(isolated.stat().st_mode | 0o111)
        assert sorted(path.name for path in work.iterdir()) == [original.name], 'Native isolation failed'
        plain = output / 'body-only.pdf'
        furnished = output / 'page-furniture.pdf'
        assert not run(isolated, first, plain, cwd=work), 'Plain native CLI wrote stdout'
        assert not run(isolated, first, furnished, '--page-furniture', config, cwd=work), 'Furniture native CLI wrote stdout'
        assert count_pages(plain) == count_pages(furnished) == 2, 'Furniture changed page count'
        furniture_results = [validate_furniture_words(page_boxes(plain, page),
                             page_boxes(furnished, page), page) for page in (1, 2)]
        repeated = output / 'furniture-repeat.pdf'
        assert not run(isolated, first, repeated, '--page-furniture', config, cwd=work), 'Repeated native CLI wrote stdout'
        assert furnished.read_bytes() == repeated.read_bytes(), 'Native furniture output not deterministic'
        invalid = work / 'invalid.json'
        invalid.write_text('{bad', encoding='utf-8')
        preserved = output / 'preserved.pdf'
        preserved.write_bytes(b'native sentinel')
        failure = subprocess.run([str(isolated), str(first), str(preserved), '--overwrite',
                                  '--page-furniture', str(invalid)], cwd=work,
                                 capture_output=True, timeout=120)
        assert failure.returncode == 2 and not failure.stdout and failure.stderr, 'Invalid furniture not rejected'
        assert preserved.read_bytes() == b'native sentinel', 'Invalid configuration modified destination'
        assert not run(isolated, first, preserved, '--overwrite', '--page-furniture', config, cwd=work), 'Recovery CLI wrote stdout'
        assert preserved.read_bytes() == furnished.read_bytes(), 'Recovery output differs'
        png_dir = work / 'png-fixtures'
        create_png_fixtures(png_dir)
        logo = png_dir / 'brand-mark.png'
        continued = output / 'continued-row.pdf'
        assert not run(isolated, long, continued, '--image', f'logo={logo}', cwd=work), 'Row native CLI wrote stdout'
        assert count_pages(continued) == 2, 'Continued row page count'
        row_result = validate_continued_row([page_lines(continued, page) for page in (1, 2)])
        again = output / 'continued-row-repeat.pdf'
        assert not run(isolated, long, again, '--image', f'logo={logo}', cwd=work), 'Repeated row native CLI wrote stdout'
        assert continued.read_bytes() == again.read_bytes(), 'Continued row not deterministic'
        for specimen, stem in ((furnished, 'page-furniture'), (continued, 'continued-row')):
            run('pdftoppm', '-f', '1', '-l', '2', '-png', '-r', '72', specimen, output / stem)
            assert len(list(output.glob(stem + '-*.png'))) == 2, 'Missing native page preview'
        assert not list(output.glob('.factspdf-*.tmp')), 'Leaked CLI temporary output'
        report = {
            'sourceHead': run('git', '-C', ROOT, 'rev-parse', 'HEAD').decode().strip(),
            'platform': __import__('platform').platform(),
            'nativeExecutableSha256': sha(original),
            'firstInputSha256': sha(first), 'configSha256': sha(config),
            'continuedInputSha256': sha(long), 'logoSha256': sha(logo),
            'furniture': {'sha256': sha(furnished), 'pages': 2, 'wordGeometry': furniture_results,
                          'bodyWordGeometryUnchanged': True, 'leftRightAndMarginPositions': True,
                          'repeatBytesEqual': True, 'invalidConfigDestinationPreserved': True,
                          'failureRecoveryBytesEqual': True},
            'continuedRow': {'sha256': sha(continued), **row_result, 'repeatBytesEqual': True},
            'gates': {'nativeProcess': True, 'independentPoppler': True,
                      'humanReferenceApproval': False, 'published': False}
        }
        (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(report))


if __name__ == '__main__':
    main()
