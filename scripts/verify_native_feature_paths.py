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


def validate_furniture_words(original, decorated, page):
    """Prove complete phrases, positions and unchanged body word geometry."""
    width, height, baseline = original
    other_width, other_height, written = decorated
    assert (width, height) == (other_width, other_height), 'Page geometry changed'
    extra = Counter(written) - Counter(baseline)
    missing = Counter(baseline) - Counter(written)
    assert not missing, ('Body word/position lost or moved', page, missing)
    expected = {'footerRight': f'Page {page} of 2'}
    if page == 2:
        expected.update(headerLeft='Business report', headerRight='Internal evaluation',
                        footerLeft='Prepared offline')
    groups = {}
    for word, left, top, right, bottom in extra.elements():
        assert 2 <= left < right <= width - 2 and 2 <= top < bottom <= height - 2, (
            'Furniture outside page clearance', page, word)
        if bottom < 50:
            band = 'header'
        elif top > height - 50:
            band = 'footer'
        else:
            raise AssertionError(('Furniture outside margin bands', page, word, top, bottom))
        if right < width / 2:
            side = 'Left'
        elif left > width / 2:
            side = 'Right'
        else:
            raise AssertionError(('Furniture not in expected side region', page, word, left, right))
        groups.setdefault(band + side, []).append((left, word, top, bottom))
    assert set(groups) == set(expected), ('Missing/extra furniture regions', page, groups)
    for region, phrase in expected.items():
        ordered = sorted(groups[region])
        assert [word for _, word, _, _ in ordered] == phrase.split(), (
            'Furniture phrase or word order changed', page, region, phrase, ordered)
        assert max(item[3] for item in ordered) - min(item[2] for item in ordered) < 14, (
            'Furniture phrase has inconsistent vertical placement', page, region)
    return dict(page=page, addedWords=sum(extra.values()),
                completePhrases=expected, bodyWordsUnchanged=len(baseline))


def expected_note_texts(html):
    """Extract the authored fixture notes, never using generated-PDF output as truth."""
    from html import unescape
    paragraphs = [re.sub(r'\s+', ' ', unescape(match.group(1))).strip()
                  for match in re.finditer(r'<p\s+class="detail"[^>]*>(.*?)</p>', html, re.DOTALL)]
    notes = [value for value in paragraphs if re.match(r'^\d{2}\.\s', value)]
    assert len(notes) == 24, 'Source fixture note count changed'
    assert [value[:3] for value in notes] == [f'{i:02d}.' for i in range(1, 25)], 'Source fixture note IDs changed'
    final = [value for value in paragraphs if value.startswith('FINAL REVIEW NOTE:')]
    assert len(final) == 1, 'Source fixture final note missing'
    return notes, final[0]


def validate_continued_row(pages, expected_notes=None, final_note=None):
    assert len(pages) == 2
    normalized = [' '.join(' '.join(page).split()) for page in pages]
    text = ' '.join(normalized)
    numbers = re.findall(r'(?<!\d)(?:0[1-9]|1[0-9]|2[0-4])\.(?=\s)', text)
    expected = [f'{number:02d}.' for number in range(1, 25)]
    assert numbers == expected, ('Continued note missing, repeated or out of order', numbers)
    if expected_notes is None:
        expected_notes = [f'{n:02d}. Note {n}' for n in range(1, 25)]
    assert len(expected_notes) == 24 and [note[:3] for note in expected_notes] == expected
    positions = []
    for index, note in enumerate(expected_notes):
        page = 0 if index < 12 else 1
        assert normalized[page].count(note) == 1 and note not in normalized[1-page], (
            'Note contents missing, duplicated or misplaced', index + 1, note)
        positions.append((page, normalized[page].index(note)))
    assert positions == sorted(positions), 'Source notes reordered'
    header = 'Description / delivery notes'
    for page in (0, 1):
        assert normalized[page].count(header) == 1, ('Repeated heading missing/duplicated on page', page + 1)
        assert normalized[page].index(header) < positions[page * 12][1], 'Heading not before continuation'
    assert normalized[0].count('SRV-001') == 1 and normalized[1].count('SRV-001') == 0, 'Initial item placement'
    assert normalized[0].count('600.00') == 1 and normalized[1].count('600.00') == 0, 'Initial amount placement'
    assert normalized[0].index('SRV-001') < positions[0][1], 'Initial item not before notes'
    # Ensure all wrapped lines appear exactly once, not just the numbered prefixes.
    for index in range(24):
        if index == 23:
            continue
        page, start = positions[index]
        stop = positions[index + 1][1] if positions[index + 1][0] == page else len(normalized[page])
        fragment = normalized[page][start:stop].strip()
        wanted = expected_notes[index] + (' 600.00' if index == 11 else '')
        assert fragment == wanted, ('Note continuation line added, omitted or reordered', index + 1, fragment)
    tail = normalized[1][positions[-1][1] + len(expected_notes[-1]):].strip()
    if final_note:
        assert tail.startswith(final_note), 'Final review note text missing or incomplete'
        exact_tail = (final_note + ' SRV-002 Document generation '
                      'Prepared the agreed searchable reports after the template review. '
                      '300.00 TOTAL / ILLUSTRATIVE 900.00 '
                      'Synthetic capability sample. Amounts are illustrative and are not a '
                      'product quote or commercial offer.')
        assert tail == exact_tail, ('Unexpected, missing or reordered final text', tail)
    following = ([final_note] if final_note else []) + [
        'SRV-002', '300.00', 'TOTAL / ILLUSTRATIVE', '900.00']
    offsets = []
    for marker in following:
        assert text.count(marker) == 1 and marker not in normalized[0], ('Following value mislocated', marker)
        assert tail.count(marker) == 1, ('Following value missing or duplicated', marker)
        offsets.append(tail.index(marker))
    assert offsets == sorted(offsets) and len(set(offsets)) == len(offsets), 'Following row/totals before final continuation'
    return dict(notes=24, completeNoteContents=True, pages=2,
                repeatedHeadersPerPage=[1, 1], followingRowsAfterContinuation=True)


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
        assert sha(long) == '3ad9fe8afbf140ef1e9e5f0c1bfafa43add2e36da6003281c4b2d9b70f4fa8a7', 'Unreviewed row fixture change'
        authored_notes, final_note = expected_note_texts(long.read_text(encoding='utf-8'))
        row_result = validate_continued_row([page_lines(continued, page) for page in (1, 2)], authored_notes, final_note)
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
