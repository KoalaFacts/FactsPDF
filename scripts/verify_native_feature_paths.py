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


def validate_row_fixture(path):
    """Pin authored bytes modulo checkout CRLF; keep raw hashes in the receipt."""
    normalized = path.read_bytes().replace(b"\r\n", b"\n")
    digest = hashlib.sha256(normalized).hexdigest()
    assert digest == "e6dd417ec5da8193124c18110a6b48575508d034c9c91cbbc3e002ef3dc26650", \
        "Unreviewed row fixture change"
    return digest


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
        groups.setdefault(band + side, []).append((left, word, top, bottom, right))
    assert set(groups) == set(expected), ('Missing/extra furniture regions', page, groups)
    for region, phrase in expected.items():
        ordered = sorted(groups[region])
        assert [item[1] for item in ordered] == phrase.split(), (
            'Furniture phrase or word order changed', page, region, phrase, ordered)
        assert all(previous[4] <= following[0] for previous, following in zip(ordered, ordered[1:])), (
            'Furniture words overlap horizontally', page, region)
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
    caption = 'Authored brand asset, kept with its own complete image border.'
    caption_counts = [page.count(caption) for page in normalized]
    assert sum(caption_counts) == 1, 'Authored caption missing, duplicated or split across pages'
    caption_on_first = caption_counts[0] == 1
    # The image/caption boundary may fit after note 12 or continue before note 13.
    # Match the complete boundary, rather than discarding unrecognized prefix text.
    second_prefix = 'Item ' + header + ' Amount' + ('' if caption_on_first else ' ' + caption)
    assert normalized[1][:positions[12][1]].strip() == second_prefix, (
        'Unexpected caption or text before second-page continuation', normalized[1][:positions[12][1]])
    # Ensure all wrapped lines appear exactly once, not just the numbered prefixes.
    for index in range(24):
        if index == 23:
            continue
        page, start = positions[index]
        stop = positions[index + 1][1] if positions[index + 1][0] == page else len(normalized[page])
        fragment = normalized[page][start:stop].strip()
        wanted = expected_notes[index]
        if index == 11:
            wanted += (' ' + caption if caption_on_first else '') + ' 600.00'
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


def list_marker_label(kind, ordinal):
    """Independent expected marker oracle, intentionally not using engine code."""
    if kind == 'alpha':
        output = ''
        while ordinal:
            ordinal -= 1
            output = chr(97 + ordinal % 26) + output
            ordinal //= 26
        return output + '.'
    output = ''
    for number, digits in ((1000, 'M'), (900, 'CM'), (500, 'D'), (400, 'CD'),
                           (100, 'C'), (90, 'XC'), (50, 'L'), (40, 'XL'),
                           (10, 'X'), (9, 'IX'), (5, 'V'), (4, 'IV'), (1, 'I')):
        while ordinal >= number:
            output += digits
            ordinal -= number
    return output + '.'


def inspect_list_markers(pages):
    """Verify real Poppler body/marker attachment, count, page and gutter geometry.

    pages is a list of (width,height,[(text,xMin,yMin,xMax,yMax),...]).
    A deliberately independent algorithm computes every expected label.
    """
    assert len(pages) >= 2, 'The 74-entry marker report must paginate'
    positioned = [(p, item) for p, (_, _, words) in enumerate(pages) for item in words]
    expected_ids = [f'CHK-{i:02d}' for i in range(1, 41)] + [
        f'ACT-{i:02d}' for i in range(1, 26)] + [
        f'NOTE-{i:02d}' for i in range(1, 10)]
    actual_ids = [w[0] for _, w in positioned if re.fullmatch(r'(?:CHK|ACT|NOTE)-\d{2}', w[0])]
    assert actual_ids == expected_ids, 'Report IDs were duplicated, removed or reordered'
    assert sum(w[0] == 'END-REPORT' for _, w in positioned) == 1, 'Missing report tail'
    groups = {}
    for prefix, count, kind in [('CHK', 40, 'alpha'), ('ACT', 25, 'roman')]:
        x_positions = []
        found_pages = set()
        for index in range(1, count + 1):
            body_id = f'{prefix}-{index:02d}'
            matches = [(p, word) for p, word in positioned if word[0] == body_id]
            assert len(matches) == 1, 'Missing body record: ' + body_id
            page, body = matches[0]
            found_pages.add(page)
            marker = list_marker_label(kind, index)
            candidates = [word for word in pages[page][2]
                          if word[0] == marker and
                          abs((word[2]+word[4]) / 2 - (body[2]+body[4]) / 2) < 4.0]
            assert len(candidates) == 1, 'Missing/duplicate or detached list marker: ' + body_id
            label = candidates[0]
            assert 0 < label[1] < label[3] < body[1]-3, 'Marker overlaps body: ' + body_id
            x_positions.append(body[1])
        assert max(x_positions) - min(x_positions) < .05, 'List gutter changed between pages: ' + prefix
        groups[prefix] = {'count': count, 'bodyX': x_positions[0], 'pages': len(found_pages)}
    notes = [word for _, word in positioned if re.fullmatch(r'NOTE-\d{2}', word[0])]
    assert len(notes) == 9
    assert max(w[1] for w in notes)-min(w[1] for w in notes) < .05
    return {'pages': len(pages), 'records': 74, 'alphaMarkers': 40,
            'romanMarkers': 25, 'markerFreeNotes': 8, 'vectorDiscOverrides': 1,
            'markerAttachmentAndGutters': groups}




def extract_vector_page_streams(pdf_bytes, page_count):
    """Read exact uncompressed page-content streams, without platform-specific qpdf."""
    object_ids = re.findall(rb'/Type /Page /Parent[^\r\n]+?/Contents (\d+) 0 R', pdf_bytes)
    assert len(object_ids) == page_count, 'Missing or extra PDF page content references'
    streams = []
    for object_id in object_ids:
        header = re.search(rb'(?m)^' + re.escape(object_id) +
                           rb' 0 obj\r?\n<< /Length (\d+) >>\r?\nstream\r?\n', pdf_bytes)
        assert header, 'Missing uncompressed PDF page stream object'
        length = int(header.group(1))
        start = header.end()
        content = pdf_bytes[start:start + length]
        assert len(content) == length and pdf_bytes[start + length:start + length + 10].startswith(
            b'\nendstream'), 'PDF content length or stream boundary mismatched'
        streams.append(content)
    return streams


def inspect_vector_marker_geometry(pages, streams):
    """Match 80 *painted* vectors to 80 Poppler labels on the same page.

    Verify exact drawn centre/extent, inherited RGB, ring stroke width and
    all four cubic control arcs. Global path counts alone accept off-page
    markers and cannot satisfy this checker.
    """
    assert len(pages) == len(streams) >= 2
    groups = {'CIR': [], 'SQR': []}
    for page_idx, ((width, height, words), content) in enumerate(zip(pages, streams)):
        expected_rows = {key: sorted(
            (w for w in words if re.fullmatch(key + r'-\d{2}', w[0])),
            key=lambda item: item[2]) for key in groups}
        painted = {'CIR': [], 'SQR': []}
        for raw_line in content.splitlines():
            parts = raw_line.split()
            if len(parts) == 40 and parts[:1] == [b'q'] and parts[4:5] == [b'RG'] \
                    and parts[6:7] == [b'w'] and parts[9:10] == [b'm'] \
                    and parts[-2:] == [b'S', b'Q']:
                red, green, blue = map(float, parts[1:4])
                assert all(abs(actual - expected) < .002 for actual, expected in
                           zip((red, green, blue), (40/255, 85/255, 122/255))), \
                    'Circle marker inherited colour differs'
                stroke = float(parts[5])
                assert abs(stroke - .45) < .002, 'Circle stroke must be one quarter of 1.8pt marker radius'
                start_x, centre_y = map(float, parts[7:9])
                radius = 1.44
                centre_x = start_x - radius
                k = radius * .5522847498307936
                expected_arcs = (
                    ((centre_x+radius, centre_y+k), (centre_x+k, centre_y+radius), (centre_x, centre_y+radius)),
                    ((centre_x-k, centre_y+radius), (centre_x-radius, centre_y+k), (centre_x-radius, centre_y)),
                    ((centre_x-radius, centre_y-k), (centre_x-k, centre_y-radius), (centre_x, centre_y-radius)),
                    ((centre_x+k, centre_y-radius), (centre_x+radius, centre_y-k), (centre_x+radius, centre_y)),
                )
                index = 10
                for arc in expected_arcs:
                    for ex, ey in arc:
                        assert abs(float(parts[index])-ex) < .003 and \
                            abs(float(parts[index+1])-ey) < .003, 'Malformed hollow circle cubic path'
                        index += 2
                    assert parts[index] == b'c', 'Incomplete hollow circle cubic path'
                    index += 1
                assert parts[index:] == [b'S', b'Q'], 'Circle must be stroke-only'
                painted['CIR'].append((centre_x, centre_y, 1.8))
            elif len(parts) == 12 and parts[:1] == [b'q'] and parts[4:5] == [b'rg'] \
                    and parts[-3:] == [b're', b'f', b'Q']:
                red, green, blue = map(float, parts[1:4])
                assert all(abs(actual-expected) < .002 for actual, expected in
                           zip((red, green, blue), (145/255, 69/255, 46/255))), \
                    'Square marker inherited colour differs'
                left, bottom, side_w, side_h = map(float, parts[5:9])
                assert abs(side_w-3.6) < .002 and abs(side_h-3.6) < .002, \
                    'Square must be one filled 3.6pt by 3.6pt marker'
                painted['SQR'].append((left + side_w/2, bottom + side_h/2, 1.8))
        for kind in groups:
            rows = expected_rows[kind]
            marks = sorted(painted[kind], key=lambda marker: height-marker[1])
            assert len(rows) == len(marks), ('Missing/duplicate vector on its actual page', kind, page_idx)
            for row, (centre_x, centre_y, r) in zip(rows, marks):
                cx_left = centre_x-r
                cx_right = centre_x+r
                top = height-centre_y
                assert 0 < cx_left < cx_right < row[1]-3 and row[1]-cx_right < 12, \
                    ('Vector horizontally detached or outside page', kind, row[0], centre_x)
                assert abs(top - (row[2]+row[4])/2) < 7.5, \
                    ('Vector vertically detached from actual item first line', kind, row[0], top)
                assert 0 < top-r < top+r < height, 'Vector drawn off-page'
                groups[kind].append((page_idx, row[0], centre_x, top))
    assert len(groups['CIR']) == len(groups['SQR']) == 40, 'Expected 40 circles and 40 squares'
    return {'circlesStroked': len(groups['CIR']), 'squaresFilled': len(groups['SQR']),
            'perPageMarkerTextAttachment': True, 'markerBoundsAndColours': True,
            'hollowFourCubicArcsAndStroke': True}


def verify_native_vector_markers(executable, output, work):
    """Verify native PDF paths and real Poppler positions, not just CSS acceptance."""
    source = ROOT / 'examples/report-list-vector-shapes.html'
    pdf = output / 'vector-marker-report.pdf'
    assert not run(executable, source, pdf, cwd=work), 'Native vector marker CLI wrote stdout'
    page_count = count_pages(pdf)
    assert page_count >= 2, 'Vector marker register must span pages'
    pages = [page_boxes(pdf, page) for page in range(1, page_count + 1)]
    words = [word for _, _, page_words in pages for word in page_words]
    expected = [f'CIR-{i:02d}' for i in range(1, 41)] + [f'SQR-{i:02d}' for i in range(1, 41)]
    actual = [word[0] for word in words if re.fullmatch(r'(?:CIR|SQR)-\d{2}', word[0])]
    assert actual == expected, 'Vector marker report text missing, duplicated or reordered'
    assert sum(word[0] == 'END-VECTOR-REPORT' for word in words) == 1
    positions = {}
    for prefix in ('CIR', 'SQR'):
        rows = [word for word in words if re.fullmatch(prefix + r'-\d{2}', word[0])]
        assert len(rows) == 40
        assert max(word[1] for word in rows) - min(word[1] for word in rows) < .05, 'Vector list gutter not stable'
        positions[prefix] = rows[0][1]
    data = pdf.read_bytes()
    streams = extract_vector_page_streams(data, page_count)
    geometry = inspect_vector_marker_geometry(pages, streams)
    assert not re.findall(rb' c f Q\n', data), 'Unrequested legacy filled discs in vector register'
    return {'pages': page_count, 'records': len(expected), **geometry,
            'bodyX': positions, 'sha256': sha(pdf),
            'popplerTextAndPdfPathEvidence': True}



def verify_native_list_continuations(executable, output, work):
    """Independent expected ordinals and real native PDF glyph geometry."""
    source = ROOT / 'examples/report-list-continuations.html'
    pdf = output / 'list-continuations.pdf'
    assert not run(executable, source, pdf, cwd=work), 'List continuation wrote stdout'
    count = count_pages(pdf)
    assert count >= 2, 'List-continuation register did not actually paginate'
    pages = [page_boxes(pdf, page) for page in range(1, count + 1)]
    words = [(page, word) for page, (_, _, entries) in enumerate(pages, 1) for word in entries]

    # Expected counters are independently enumerated here; they are not
    # inferred from the produced PDF or from the HTML renderer's model.
    def roman(n):
        result = ''
        for value, symbol in ((1000,'M'),(900,'CM'),(500,'D'),(400,'CD'),
                              (100,'C'),(90,'XC'),(50,'L'),(40,'XL'),
                              (10,'X'),(9,'IX'),(5,'V'),(4,'IV'),(1,'I')):
            while n >= value:
                result += symbol
                n -= value
        return result
    groups = {
        'RC-A': [str(20 + i) + '.' if i < 10 else str(43 + i - 10) + '.'
                 for i in range(1, 27)],
        'RC-B': [roman(21 - i) + '.' for i in range(1, 21)],
        'RC-C': [str(47 - i) + '.' if i < 8 else str(31 - (i - 8)) + '.'
                 for i in range(1, 26)]
    }
    for prefix, labels in groups.items():
        body_x = []
        for index, expected in enumerate(labels, 1):
            name = f'{prefix}{index:02d}'
            found = [(page, box) for page, box in words if box[0] == name]
            assert len(found) == 1, ('Lost or repeated body entry', name, found)
            page, body = found[0]
            markers = [box for marker_page, box in words
                       if marker_page == page and box[0] == expected
                       and abs(box[2] - body[2]) < 2.0 and box[3] < body[1]]
            assert len(markers) == 1, ('Wrong, repeated or detached ordinal', name, expected, markers)
            body_x.append(body[1])
        assert max(body_x) - min(body_x) < .05, ('Inconsistent marker gutter', prefix, body_x)
    for index in range(1, 7):
        name = f'RC-D{index:02d}'
        assert sum(box[0] == name for _, box in words) == 1, ('Missing marker-free entry', name)
    for name, html, code in (
        ('zero', '<ol start="0"><li>A</li></ol>', 'FPDF1103'),
        ('reverse-underflow', '<ol reversed start="1"><li>A</li><li>B</li></ol>', 'FPDF1103'),
        ('wrong-target', '<ul><li value="4">A</li></ul>', 'FPDF1103'),
        ('unsupported-position', '<ol style="list-style:circle inside"><li>A</li></ol>', 'FPDF1202'),
    ):
        bad = output / f'list-control-{name}.html'
        dest = output / f'list-control-{name}.pdf'
        bad.write_text(html, encoding='utf-8')
        dest.write_bytes(b'list-control sentinel')
        failed = subprocess.run([str(executable), str(bad), str(dest), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code.encode() in failed.stderr and not failed.stdout, (
            'Native list control failure', name, failed.returncode, failed.stderr)
        assert dest.read_bytes() == b'list-control sentinel', 'Native failure clobbered output'
        assert not run(executable, source, dest, '--overwrite', cwd=work)
        assert dest.read_bytes() == pdf.read_bytes(), 'Native recovery changed valid PDF'
    return {'pages': count, 'numberedItems': sum(map(len, groups.values())),
            'markerFreeItems': 6, 'sha256': sha(pdf), 'sourceSha256': sha(source),
            'popplerOrdinalAndGeometry': True, 'protectedFailureRecovery': True}


def verify_native_list_markers(executable, root, output, work):
    source = ROOT / 'examples/report-list-marker-styles.html'
    assert source.is_file(), 'Missing authored list marker report'
    pdf = output / 'list-marker-report.pdf'
    assert not run(executable, source, pdf, cwd=work), 'Native marker conversion wrote stdout'
    page_count = count_pages(pdf)
    pages = [page_boxes(pdf, page) for page in range(1, page_count + 1)]
    evidence = inspect_list_markers(pages)
    evidence.update({'sha256': sha(pdf), 'sourceSha256': sha(source),
                     'nativePdf': True, 'nativePdfTextPositions': True})
    for name, html, code in [
        ('unknown-type', '<ol style="list-style-type:diamond"><li>A</li></ol>', 'FPDF1202'),
        ('invalid-element', '<p style="list-style-type:upper-roman">A</p>', 'FPDF1201')
    ]:
        bad = output / f'list-marker-{name}.html'
        dest = output / f'list-marker-{name}.pdf'
        bad.write_text(html, encoding='utf-8')
        dest.write_bytes(b'marker sentinel')
        failure = subprocess.run([str(executable), str(bad), str(dest), '--overwrite'],
                                 cwd=work, capture_output=True, timeout=120)
        assert failure.returncode == 3 and code.encode() in failure.stderr and not failure.stdout, (
            'Native CSS marker error contract', name, failure.returncode, failure.stderr)
        assert dest.read_bytes() == b'marker sentinel', 'Native marker failure replaced an existing PDF'
        assert not run(executable, source, dest, '--overwrite', cwd=work), 'Native recovery wrote stdout'
        assert dest.read_bytes() == pdf.read_bytes(), 'Marker error recovery altered report PDF bytes'
    evidence['negativeAndRecovery'] = ['unknown-type', 'invalid-element']
    evidence['vectorShapes'] = verify_native_vector_markers(executable, output, work)
    evidence['numberedContinuation'] = verify_native_list_continuations(executable, output, work)
    return evidence



def verify_native_multiline_notes(executable, output, work):
    """Compare native rendered LF/CR paragraph breaks with separately authored HTML br."""
    source = ROOT / 'examples/report-pre-line-notes.html'
    oracle = ROOT / 'examples/report-pre-line-notes-oracle.html'
    pdf = output / 'multiline-report.pdf'
    expected = output / 'multiline-oracle.pdf'
    assert source.is_file() and oracle.is_file(), 'Missing authored multiline report pair'
    assert not run(executable, source, pdf, cwd=work)
    assert not run(executable, oracle, expected, cwd=work)
    assert pdf.read_bytes() == expected.read_bytes(), 'Native pre-line did not produce physical br-equivalent PDF'
    count = count_pages(pdf)
    assert count >= 2, '72 multiline report notes must paginate'
    pages = [page_boxes(pdf, p) for p in range(1, count + 1)]
    actual = [word for _, _, words in pages for word in words]
    oracle_words = [word for p in range(1, count + 1) for word in page_boxes(expected, p)[2]]
    assert actual == oracle_words, 'Native text positions differ from explicit-br oracle'
    labels = [word[0] for word in actual]
    for i in range(1, 73):
        assert labels.count(f'REF{i:03d}') == 1, ('Missing or duplicate source note', i)
    assert labels.count('FINALCHECK') == 1, 'Missing closing summary'
    for name, html, code in (
        ('invalid-mode', '<p style="white-space:break-spaces">BAD</p>', 'FPDF1202'),
        ('overwide-note', '<p style="width:5pt;white-space:pre-line">LONGIDENTIFIER</p>', 'FPDF1302')):
        bad = output / ('multiline-' + name + '.html')
        dest = output / ('multiline-' + name + '.pdf')
        bad.write_text(html, encoding='utf-8')
        dest.write_bytes(b'original-note-pdf')
        failed = subprocess.run([str(executable), str(bad), str(dest), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code.encode() in failed.stderr and not failed.stdout, (
            'Native multiline note error contract', name, failed.returncode, failed.stderr)
        assert dest.read_bytes() == b'original-note-pdf', 'Native note failure clobbered output'
        assert not run(executable, source, dest, '--overwrite', cwd=work)
        assert dest.read_bytes() == pdf.read_bytes(), 'Native recovery changed report PDF bytes'
    return {'pages': count, 'references': 72, 'textPositionsMatchOracle': True,
            'pdfBytesMatchOracle': True, 'protectedFailureRecovery': True,
            'pdfSha256': sha(pdf), 'sourceSha256': sha(source),
            'oracleSha256': sha(oracle)}



def verify_native_preserved_space_notes(executable, output, work):
    """Read two separately authored reports with Poppler; prove real space advances."""
    source = ROOT / 'examples/report-preserved-space-notes.html'
    oracle = ROOT / 'examples/report-preserved-space-notes-oracle.html'
    pdf = output / 'preserved-space-report.pdf'
    collapsed = output / 'preserved-space-control.pdf'
    assert source.is_file() and oracle.is_file(), 'Missing authored pre-wrap report pair'
    assert not run(executable, source, pdf, cwd=work), 'Native preserved-space output wrote stdout'
    assert not run(executable, oracle, collapsed, cwd=work), 'Native control wrote stdout'
    assert pdf.read_bytes() != collapsed.read_bytes(), 'Preserved spaces were silently collapsed'
    count = count_pages(pdf)
    assert count >= 2 and count == count_pages(collapsed), '72 notes must paginate equally'
    actual_pages = [page_boxes(pdf, p) for p in range(1, count + 1)]
    control_pages = [page_boxes(collapsed, p) for p in range(1, count + 1)]
    actual = [word for _, _, words in actual_pages for word in words]
    control = [word for _, _, words in control_pages for word in words]
    labels = [word[0] for word in actual]
    for i in range(1, 73):
        assert labels.count(f'REF{i:03d}') == 1, ('Missing or duplicated report note', i)
    assert labels.count('ASSET') == labels.count('CHECK') == labels.count('OWNER:') == 72, (
        'Missing report note words', Counter(labels))
    assert labels.count('FINALCHECK') == 1, 'Missing final note'
    def locations(words, name):
        return [(x, y) for value, x, y, _, _ in words if value == name]
    for name in ('ASSET', 'CHECK', 'OWNER:'):
        real = locations(actual, name)
        baseline = locations(control, name)
        assert len(real) == len(baseline) == 72, ('Missing control words', name)
        assert all(left > old_left + 3 for (left, _), (old_left, _) in zip(real, baseline)), (
            'Preserved spaces did not advance PDF text position', name, real, baseline)
        assert all(abs(y - old_y) < 0.01 for (_, y), (_, old_y) in zip(real, baseline)), (
            'Preserved spaces altered authored line baselines unexpectedly', name)
    for name, html, code in (
        ('tab', '<p style="white-space:pre-wrap">AB\tBA</p>', 'FPDF1305'),
        ('unsupported', '<p style="white-space:break-spaces">AB</p>', 'FPDF1202'),
        ('too-wide', '<p style="width:5pt;white-space:pre-wrap">LONGIDENTIFIER</p>', 'FPDF1302'),
    ):
        bad = output / f'prewrap-{name}.html'
        dest = output / f'prewrap-{name}.pdf'
        bad.write_text(html, encoding='utf-8')
        dest.write_bytes(b'original-preserved-pdf')
        failed = subprocess.run([str(executable), str(bad), str(dest), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code.encode() in failed.stderr and not failed.stdout, (
            'Native preserved-space failure contract', name, failed.returncode, failed.stderr)
        assert dest.read_bytes() == b'original-preserved-pdf', (
            'Native failed conversion modified existing PDF')
        assert not run(executable, source, dest, '--overwrite', cwd=work)
        assert dest.read_bytes() == pdf.read_bytes(), 'Recovery changed preserved-space PDF'
    return {'pages': count, 'references': 72, 'nativePopplerTextGeometry': True,
            'independentCollapsedSpaceControl': True, 'outputPreservedOnFailure': True,
            'sourceSha256': sha(source), 'controlSha256': sha(oracle), 'pdfSha256': sha(pdf)}


def verify_native_patterned_borders(executable, output, work):
    """Independently inspect actual Native AOT PDF strokes and text geometry."""
    source = ROOT / 'examples/report-patterned-borders.html'
    assert source.is_file(), 'Missing authored patterned-border report'
    pdf = output / 'patterned-border-report.pdf'
    assert not run(executable, source, pdf, cwd=work), 'Native patterned-border conversion wrote stdout'
    count = count_pages(pdf)
    assert count >= 2, 'Authored 76-record report must span pages'
    pages = [page_boxes(pdf, page) for page in range(1, count + 1)]
    words = [word[0] for _, _, entries in pages for word in entries]
    for i in range(1, 77):
        label = f'REF{i:03d}'
        assert words.count(label) == 1, ('Missing or repeated inspection reference', label)
    assert words.count('FINALCHECK') == 1, 'Missing final painted summary'
    streams = extract_vector_page_streams(pdf.read_bytes(), count)
    dashed = dotted = 0
    for page_idx, (stream, page) in enumerate(zip(streams, pages)):
        width, height, _ = page
        assert b'nan' not in stream.lower(), 'Invalid native PDF border geometry'
        for line in stream.splitlines():
            if not line.startswith(b'q ') or not line.endswith(b' l S Q'):
                continue
            parts = line.split()
            if b'd' not in parts or b'RG' not in parts or b'w' not in parts:
                continue
            style = ('dotted' if b'1 J [0.001 4] 0 d' in line
                     else 'dashed' if b'[6 6] 0 d' in line else 'unknown')
            assert style != 'unknown', ('Unexpected native border stroke pattern', line)
            assert b' re W n ' in line, ('Patterned border lacks measured-box clipping', line)
            values = [float(parts[index]) for index in
                      (parts.index(b'm')-2, parts.index(b'm')-1,
                       parts.index(b'l')-2, parts.index(b'l')-1)]
            x1, y1, x2, y2 = values
            assert all(0 <= x <= width for x in (x1, x2)) and                    all(0 <= y <= height for y in (y1, y2)),                    ('Off-page patterned border stroke', page_idx, values)
            if style == 'dashed':
                dashed += 1
            else:
                dotted += 1
    assert dashed >= count * 2, ('Lost paginated dashed vertical border strokes', dashed, count)
    assert dotted == 4, ('Expected four dotted summary border strokes', dotted)
    for name, css, code in [('invalid-pattern', '2pt double red', 'FPDF1202'),
                            ('overflow', '4pt dotted red;width:900pt', 'FPDF1302')]:
        bad = output / f'patterned-border-{name}.html'
        dest = output / f'patterned-border-{name}.pdf'
        bad.write_text('<div style="border:' + css + '">FAIL</div>', encoding='utf-8')
        dest.write_bytes(b'pattern sentinel')
        failed = subprocess.run([str(executable), str(bad), str(dest), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code.encode() in failed.stderr and not failed.stdout, (
            'Native patterned border error contract', name, failed.returncode, failed.stderr)
        assert dest.read_bytes() == b'pattern sentinel', 'Native failure replaced prior output'
        assert not run(executable, source, dest, '--overwrite', cwd=work)
        assert dest.read_bytes() == pdf.read_bytes(), 'Native recovery changed valid PDF bytes'
    return {'pages': count, 'references': 76, 'dashedPaths': dashed,
            'dottedPaths': dotted, 'textAndGeometry': True,
            'protectedFailureRecovery': True,
            'pdfSha256': sha(pdf), 'sourceSha256': sha(source)}



def verify_native_preformatted_audit(executable, output, work):
    """Real Native AOT source notes, PDF text/geometry and protected failure recovery."""
    source = ROOT / 'examples/report-preformatted-audit.html'
    oracle = ROOT / 'examples/report-preformatted-audit-oracle.html'
    assert source.is_file() and oracle.is_file(), 'Missing preformatted audit examples'
    pdf = output / 'preformatted-audit.pdf'
    control = output / 'preformatted-audit-oracle.pdf'
    assert not run(executable, source, pdf, cwd=work)
    assert not run(executable, oracle, control, cwd=work)
    assert pdf.read_bytes() == control.read_bytes(), 'Native pre differs from independent pre-wrap oracle'
    pages = count_pages(pdf)
    assert pages >= 2, 'Authored 72-row audit must span pages'
    seen = [word for page in range(1, pages + 1) for word in page_lines(pdf, page)]
    for index in range(1, 73):
        label = f'R{index:03d}'
        assert sum(label in line.split() for line in seen) == 1, ('Missing or duplicate preformatted row', label)
    assert any('FINAL' in line for line in seen), 'Final audit line disappeared'

    spaced = output / 'preformatted-spacing.html'
    single = output / 'preformatted-single-space.html'
    spaced.write_text('<pre>AB   BA</pre>', encoding='utf-8')
    single.write_text('<pre>AB BA</pre>', encoding='utf-8')
    gap_pdf = output / 'preformatted-spacing.pdf'
    single_pdf = output / 'preformatted-single-space.pdf'
    assert not run(executable, spaced, gap_pdf, cwd=work)
    assert not run(executable, single, single_pdf, cwd=work)
    def second_word_x(path):
        words = page_boxes(path, 1)[2]
        match = [word for word in words if word[0] == 'BA']
        assert len(match) == 1, ('Missing PDF bbox spacing word', path, words)
        return match[0][1]
    delta = second_word_x(gap_pdf) - second_word_x(single_pdf)
    assert 13.5 < delta < 15.5, ('Literal U+0020 glyph advances missing in PDF', delta)

    invalid = output / 'preformatted-tab.html'
    invalid.write_text('<pre>AB&#9;BA</pre>', encoding='utf-8')
    dest = output / 'preformatted-recovery.pdf'
    dest.write_bytes(b'preformatted sentinel')
    result = subprocess.run([str(executable), str(invalid), str(dest), '--overwrite'],
                            cwd=work, capture_output=True, timeout=120)
    assert result.returncode == 3 and b'FPDF1305' in result.stderr and not result.stdout, (
        'Native tab-stop rejection failed', result.returncode, result.stderr)
    assert dest.read_bytes() == b'preformatted sentinel', 'Failed native pre replaced previous PDF'
    assert not run(executable, source, dest, '--overwrite', cwd=work)
    assert dest.read_bytes() == pdf.read_bytes(), 'Native pre recovery changed valid PDF bytes'
    return {'pages': pages, 'rows': 72, 'spaceDeltaPt': round(delta, 3),
            'independentOracleBytesEqual': True, 'protectedFailureRecovery': True,
            'pdfSha256': sha(pdf), 'sourceSha256': sha(source)}

def verify_native_table_vertical_align(executable, output, work):
    """Native real PDF witness with an independently specified physical-padding control."""
    source = ROOT / 'examples/report-table-vertical-align.html'
    oracle = ROOT / 'examples/report-table-vertical-align-oracle.html'
    assert source.is_file() and oracle.is_file()
    pdf = output / 'table-vertical-align.pdf'
    control = output / 'table-vertical-align-control.pdf'
    assert not run(executable, source, pdf, cwd=work)
    assert not run(executable, oracle, control, cwd=work)
    assert pdf.read_bytes() == control.read_bytes(), (
        'PDF bytes differ from the physical-padding vertical placement oracle')
    pages = count_pages(pdf)
    assert pages >= 2, 'Forty whole table rows must paginate'
    words = [word for page in range(1, pages + 1)
             for word in page_boxes(pdf, page)[2]]
    for number in range(1, 41):
        row = f'R{number:03d}'
        target = f'B{number:03d}' if number % 2 else f'C{number:03d}'
        label = [word for word in words if word[0] == row]
        reference = [word for word in words if word[0] == target]
        assert len(label) == len(reference) == 1, (
            'Missing or duplicate aligned business record', row, target)
        assert abs(label[0][2] - reference[0][2]) < .25, (
            'PDF text vertical position differs from specified alignment', row, target)
    assert sum(word[0] == 'Record' for word in words) == pages, 'Repeated header mismatch'
    assert sum(word[0] == 'END' for word in words) == 1, 'Missing report tail'
    invalid = output / 'table-vertical-align-invalid.html'
    invalid.write_text(source.read_text(encoding='utf-8').replace(
        'vertical-align:middle', 'vertical-align:baseline', 1), encoding='utf-8')
    preserved = output / 'table-vertical-align-preserved.pdf'
    preserved.write_bytes(b'previous PDF')
    failure = subprocess.run([str(executable), str(invalid), str(preserved), '--overwrite'],
                             cwd=work, capture_output=True, timeout=120)
    assert failure.returncode == 3 and b'FPDF1202' in failure.stderr, (
        'Invalid native vertical-align was not rejected', failure.returncode, failure.stderr)
    assert preserved.read_bytes() == b'previous PDF', 'Invalid input replaced original output'
    assert not run(executable, source, preserved, '--overwrite', cwd=work)
    assert preserved.read_bytes() == pdf.read_bytes(), 'Valid native retry differs'
    return {'pages': pages, 'rows': 40, 'textPositionsVerified': True,
            'mixedMiddleBottom': True, 'physicalPaddingPdfBytesEqual': True,
            'failureRecovery': True, 'pdfSha256': sha(pdf), 'sourceSha256': sha(source)}


def verify_native_decoration_paint(executable, output, work):
    """Verify 60 authored report rows and real clipped vector ink in Native AOT PDF."""
    source = ROOT / 'examples/report-text-decoration-paint.html'
    assert source.is_file()
    authored = source.read_text(encoding='utf-8')
    assert len(re.findall(r'<p class="entry"', authored)) == 60
    pdf = output / 'text-decoration-paint.pdf'
    assert not run(executable, source, pdf, cwd=work)
    pages = count_pages(pdf)
    assert pages >= 2, 'Source report must exercise text decoration across pages'
    words = [word for index in range(1, pages + 1)
             for word in page_boxes(pdf, index)[2]]
    assert [word[0] for word in words if re.fullmatch(r'R\d{3}', word[0])] == [
        f'R{number:03d}' for number in range(1, 61)], 'PDF record order/count differs from source'
    assert sum(word[0] == 'FINALCHECK' for word in words) == 1, 'Missing final painted note'
    streams = extract_vector_page_streams(pdf.read_bytes(), pages)
    paths = b'\n'.join(streams)
    dotted = paths.count(b'1 J [0.001 ')
    patterned = paths.count(b'] 0 d ')
    dashed = patterned - dotted  # dotted markers share the trailing '] 0 d' operator
    clipped = paths.count(b' re W n ')
    solid = paths.count(b' re f Q')
    assert dashed >= 20 and dotted >= 21 and solid >= 20, (
        'PDF is missing decorated glyph-run paint', dashed, dotted, solid)
    assert dashed >= 0 and clipped == patterned, 'Every pattern must be clipped exactly once'
    assert b'0.686 0.125 0.251 RG ' in paths, 'Red source decoration ink not painted'
    assert b'0 0.376 0.627 RG ' in paths, 'Blue source decoration ink not painted'
    assert b'0.086 0.502 0.286 rg ' in paths, 'Green solid decoration ink not painted'
    bad = output / 'text-decoration-paint-invalid.html'
    bad.write_text(authored.replace('underline dashed', 'underline wavy', 1), encoding='utf-8')
    preserved = output / 'text-decoration-paint-preserved.pdf'
    preserved.write_bytes(b'previous pdf bytes')
    failure = subprocess.run([str(executable), str(bad), str(preserved), '--overwrite'],
                             cwd=work, capture_output=True, timeout=120)
    assert failure.returncode == 3 and b'FPDF1202' in failure.stderr, (
        'Unsupported decoration was accepted', failure.returncode, failure.stderr)
    assert preserved.read_bytes() == b'previous pdf bytes', 'Invalid input overwrote PDF'
    assert not run(executable, source, preserved, '--overwrite', cwd=work)
    assert preserved.read_bytes() == pdf.read_bytes(), 'Native retry changed PDF bytes'
    return {'pages': pages, 'rows': 60, 'realPdfPathStrokes': clipped + solid,
            'redBlueGreenInk': True, 'protectedFailureRecovery': True,
            'pdfSha256': sha(pdf), 'sourceSha256': sha(source)}



def verify_native_rounded_panels(executable, output, work):
    """Proof of actual authored curved PDF cards via Native AOT and Poppler."""
    source = ROOT / 'examples/report-rounded-panels.html'
    control = ROOT / 'examples/report-rounded-panels-oracle.html'
    pdf = output / 'rounded-panels.pdf'
    oracle = output / 'rounded-panels-oracle.pdf'
    assert not run(executable, source, pdf, cwd=work)
    assert not run(executable, control, oracle, cwd=work)
    assert pdf.read_bytes() == oracle.read_bytes(), '12pt and 16px radius must have identical PDF bytes'
    assert run('qpdf', '--check', pdf) is not None
    count = count_pages(pdf)
    assert count >= 3, '54 customer cards must continue across real pages'
    words = ' '.join(' '.join(page_lines(pdf, page)) for page in range(1, count + 1))
    for index in range(1, 55):
        marker = f'Facility card {index:02d}'
        assert words.count(marker) == 1, ('Missing/repeated rounded PDF card', marker)
    assert 'Signed and issued for customer review.' in words
    content = pdf.read_bytes()
    assert b' c ' in content and b'f* Q' in content, 'Real Bezier outer/inner border paths absent'
    # A non-rounded PDF with the same text cannot accidentally satisfy this
    # geometry witness: the original solid-rectangle paint never emits f*.
    bad = output / 'rounded-panels-invalid.html'
    retained = output / 'rounded-panels-preserved.pdf'
    for html, diagnostic in (
        ("<section style='border-radius:10%'>Bad radius</section>", b'FPDF1202'),
        ("<section style='border-radius:12pt;border:2pt dashed red'>Bad border</section>", b'FPDF1201'),
        ("<span style='border-radius:12pt'>Bad inline</span>", b'FPDF1201'),
    ):
        bad.write_text(html, encoding='utf-8')
        retained.write_bytes(b'previous customer report')
        failed = subprocess.run([str(executable), str(bad), str(retained), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and diagnostic in failed.stderr and not failed.stdout, (
            'Unsupported rounded input accepted', html, failed.returncode, failed.stderr)
        assert retained.read_bytes() == b'previous customer report', 'Invalid input replaced PDF'
    assert not run(executable, source, retained, '--overwrite', cwd=work)
    assert retained.read_bytes() == content, 'Native recovery changed rounded report'
    # Distinct corner lengths must reach the native PDF painter, not just
    # parse as CSS. Independently authored 16/12/8/4px projection proves
    # physical geometry and byte-for-byte CLI parity across several pages.
    asymmetric_source = ROOT / 'examples/report-asymmetric-rounded-panels.html'
    asymmetric_control = ROOT / 'examples/report-asymmetric-rounded-panels-oracle.html'
    asymmetric = output / 'asymmetric-panels.pdf'
    asymmetric_oracle = output / 'asymmetric-panels-oracle.pdf'
    assert not run(executable, asymmetric_source, asymmetric, cwd=work)
    assert not run(executable, asymmetric_control, asymmetric_oracle, cwd=work)
    assert asymmetric.read_bytes() == asymmetric_oracle.read_bytes(), 'Asymmetric physical corner oracle differs'
    assert asymmetric.read_bytes() != content, 'Distinct corner source must not flatten to original PDF'
    assert run('qpdf', '--check', asymmetric) is not None
    asymmetric_pages = count_pages(asymmetric)
    assert asymmetric_pages >= 3, '50 rounded report entries should paginate'
    asymmetric_words = ' '.join(' '.join(page_lines(asymmetric, p))
                               for p in range(1, asymmetric_pages + 1))
    for index in range(1, 51):
        marker = f'Receipt card {index:02d}'
        assert asymmetric_words.count(marker) == 1, ('Missing/repeated asymmetric report card', marker)
    assert b' c ' in asymmetric.read_bytes() and b'f* Q' in asymmetric.read_bytes(), 'Native asymmetric paths absent'
    # The 50-card longhand version must render byte-for-byte as the
    # independently authored four-corner shorthand, including pagination.
    corner_source = ROOT / 'examples/report-corner-longhands.html'
    corner_pdf = output / 'corner-longhands.pdf'
    assert not run(executable, corner_source, corner_pdf, cwd=work)
    assert corner_pdf.read_bytes() == asymmetric.read_bytes(), 'Per-corner longhands did not paint identical PDF geometry'
    assert run('qpdf', '--check', corner_pdf) is not None
    for invalid in ('<section style="border-top-left-radius:9%">Invalid</section>',
                    '<section style="border-bottom-right-radius:8pt 4pt">Invalid</section>',
                    '<span style="border-top-right-radius:5pt">Invalid inline</span>',
                    '<section style="border-radius:10pt 2pt 4pt 1pt 3pt">Invalid</section>',
                    '<section style="border-radius:10pt 2%">Invalid</section>'):
        bad.write_text(invalid, encoding='utf-8')
        retained.write_bytes(b'previous customer report')
        failed = subprocess.run([str(executable), str(bad), str(retained), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        expected = b'FPDF1201' if 'Invalid inline' in invalid else b'FPDF1202'
        assert failed.returncode == 3 and expected in failed.stderr and not failed.stdout
        assert retained.read_bytes() == b'previous customer report'
    return {'cards': 54, 'pages': count, 'pdfSha256': sha(pdf),
            'sourceSha256': sha(source), 'oracleSha256': sha(control),
            'nativePdfByteParity': True, 'realBezierAndBorderRing': True,
            'asymmetricCards': 50, 'asymmetricPages': asymmetric_pages,
            'asymmetricPdfSha256': sha(asymmetric),
            'asymmetricPdfByteParity': True,
            'cornerLonghandCards': 50, 'cornerLonghandPdfSha256': sha(corner_pdf),
            'cornerLonghandByteParity': True,
            'protectedFailureRecovery': True}

def verify_native_minimum_height(executable, output, work):
    """Independent physical-spacer PDF oracle for bounded native panel height."""
    source = ROOT / 'examples/report-min-height-cards-native.html'
    oracle = ROOT / 'examples/report-min-height-cards-oracle.html'
    assert source.is_file() and oracle.is_file()
    pdf = output / 'minimum-height-cards.pdf'
    control = output / 'minimum-height-cards-control.pdf'
    assert not run(executable, source, pdf, cwd=work)
    assert not run(executable, oracle, control, cwd=work)
    assert pdf.read_bytes() == control.read_bytes(), (
        'Min-height card PDF differs from independent explicit vertical spacer oracle')
    assert run('qpdf', '--check', pdf) is not None
    pages = count_pages(pdf)
    assert pages >= 3, 'Forty minimum-height report entries must paginate'
    words = [word for page in range(1, pages + 1)
             for word in page_boxes(pdf, page)[2]]
    labels = [word[0] for word in words if re.fullmatch(r'CHK\d{3}', word[0])]
    assert labels == [f'CHK{i:03d}' for i in range(1, 41)], (
        'Source report card labels missing, duplicated or reordered', labels)
    assert b' re f Q' in pdf.read_bytes(), 'Minimum-height painted cards missing vector rectangles'
    bad = output / 'minimum-height-invalid.html'
    retained = output / 'minimum-height-preserved.pdf'
    for html, code in (
        ('<section style="min-height:15%">Invalid</section>', b'FPDF1202'),
        ('<table style="min-height:10pt"><colgroup><col style="width:100pt"></colgroup><tbody><tr><td>A</td></tr></tbody></table>', b'FPDF1201'),
        ('<section style="min-height:30000pt">Invalid</section>', b'FPDF1202'),
    ):
        bad.write_text(html, encoding='utf-8')
        retained.write_bytes(b'unchanged customer PDF')
        failed = subprocess.run([str(executable), str(bad), str(retained), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code in failed.stderr and not failed.stdout, (
            'Invalid native panel minimum accepted', failed.returncode, failed.stderr)
        assert retained.read_bytes() == b'unchanged customer PDF', 'Native failed input overwrote PDF'
    assert not run(executable, source, retained, '--overwrite', cwd=work)
    assert retained.read_bytes() == pdf.read_bytes(), 'Native recovery changed minimum-height PDF'
    return {'cards': 40, 'pages': pages, 'sourceSha256': sha(source),
            'oracleSha256': sha(oracle), 'pdfSha256': sha(pdf),
            'nativePdfByteParity': True, 'realPaintAndSearchableLabels': True,
            'protectedFailureRecovery': True}



def verify_native_fixed_height(executable, output, work):
    """Independently inspect actual native fixed-height report pages and recovery."""
    source = ROOT / 'examples/report-fixed-height-cards.html'
    oracle = ROOT / 'examples/report-fixed-height-cards-oracle.html'
    assert source.is_file() and oracle.is_file()
    actual = output / 'fixed-height-cards.pdf'
    expected = output / 'fixed-height-cards-control.pdf'
    assert not run(executable, source, actual, cwd=work)
    assert not run(executable, oracle, expected, cwd=work)
    run('qpdf', '--check', actual)
    run('qpdf', '--check', expected)
    assert actual.read_bytes() == expected.read_bytes(), (
        'Native fixed-height report changed glyph or painted-box geometry against bounded oracle')
    pages = count_pages(actual)
    assert pages >= 3, 'Forty fixed-height report cards must paginate'
    words = [word for page in range(1, pages + 1)
             for word in page_boxes(actual, page)[2]]
    labels = [word[0] for word in words if re.fullmatch(r'CHK\d{3}', word[0])]
    assert labels == [f'CHK{i:03d}' for i in range(1, 41)], (
        'Fixed-height labels missing, duplicated or reordered', labels)
    assert b' re f Q' in actual.read_bytes(), 'Native fixed-height backgrounds not painted'
    invalid = work / 'fixed-height-invalid.html'
    retained = output / 'fixed-height-preserved.pdf'
    for html, code in (
        ('<section style="height:30%">Invalid</section>', b'FPDF1202'),
        ('<p style="height:40pt">Invalid</p>', b'FPDF1201'),
        ('<section style="height:5pt"><p style="margin:0">Too tall</p></section>', b'FPDF1302'),
    ):
        invalid.write_text(html, encoding='utf-8')
        retained.write_bytes(b'previous customer PDF')
        failed = subprocess.run([str(executable), str(invalid), str(retained), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code in failed.stderr and not failed.stdout, (
            'Invalid native fixed-height input was accepted', failed.returncode, failed.stderr)
        assert retained.read_bytes() == b'previous customer PDF', 'Native fixed-height failure damaged PDF'
    assert not run(executable, source, retained, '--overwrite', cwd=work)
    assert retained.read_bytes() == actual.read_bytes(), 'Native fixed-height recovery changed output'
    return {'cards': 40, 'pages': pages, 'sourceSha256': sha(source),
            'oracleSha256': sha(oracle), 'pdfSha256': sha(actual),
            'nativePdfByteParity': True, 'realPaintAndSearchableLabels': True,
            'protectedFailureRecovery': True}


def verify_native_section_page_breaks(executable, output, work):
    """Real native HTML-to-PDF section boundaries against a separately authored CSS oracle."""
    original = ROOT / 'examples/report-section-page-breaks.html'
    control = ROOT / 'examples/report-section-page-breaks-oracle.html'
    assert original.is_file() and control.is_file()
    actual = output / 'section-page-breaks.pdf'
    expected = output / 'section-page-breaks-oracle.pdf'
    assert not run(executable, original, actual, cwd=work)
    assert not run(executable, control, expected, cwd=work)
    run('qpdf', '--check', actual)
    run('qpdf', '--check', expected)
    assert actual.read_bytes() == expected.read_bytes(), (
        'Native legacy page-break alias differs from independent modern PDF rendering')
    assert count_pages(actual) == 3, 'Print section boundaries did not create exactly three PDF pages'
    markers = [
        ('Facilities report', 'Summary and approval cover.'),
        ('Inspection checklist', 'Fire exit: passed', 'Lighting: maintenance scheduled'),
        ('Sign-off', 'Reviewed by facilities manager.'),
    ]
    for page, phrases in enumerate(markers, 1):
        extracted = ' '.join(page_lines(actual, page))
        _, _, positioned = page_boxes(actual, page)
        assert positioned, ('Page has no independently measured searchable glyphs', page)
        for phrase in phrases:
            assert phrase in extracted, ('Print section text missing from authored page', page, phrase)
        for other, other_markers in enumerate(markers, 1):
            if page != other:
                for phrase in other_markers:
                    assert phrase not in extracted, ('Section text leaked across PDF pages', page, phrase)
    # The legacy and modern templates must *both* differ from the
    # unbroken three-card report, not merely produce matching CSS ASTs.
    unbroken = work / 'section-breaks-control.html'
    unbroken.write_text(original.read_text(encoding='utf-8').replace(
        'page-break-before: always;', 'break-before: auto;').replace(
        'break-after: page;', 'break-after: auto;'), encoding='utf-8')
    unbroken_pdf = output / 'section-breaks-unbroken.pdf'
    assert not run(executable, unbroken, unbroken_pdf, cwd=work)
    run('qpdf', '--check', unbroken_pdf)
    assert actual.read_bytes() != unbroken_pdf.read_bytes(), 'Page breaks failed to affect PDF geometry'
    assert count_pages(unbroken_pdf) < 3, 'Control without page boundaries should fit on fewer pages'
    invalid = work / 'section-breaks-invalid.html'
    preserved = output / 'section-breaks-preserved.pdf'
    for rejected in (
        "<section style='page-break-before:left'>NO</section>",
        "<section style='page-break-after:right'>NO</section>",
        "<ol style='break-before:page'><li>NO</li></ol>",
    ):
        invalid.write_text(rejected, encoding='utf-8')
        preserved.write_bytes(b'previous customer PDF')
        failure = subprocess.run([str(executable), str(invalid), str(preserved), '--overwrite'],
                                 cwd=work, capture_output=True, timeout=120)
        diagnostic = b'FPDF1201' if rejected.startswith('<ol') else b'FPDF1202'
        assert failure.returncode == 3 and diagnostic in failure.stderr and not failure.stdout, (
            'Invalid native section boundary was accepted', rejected, failure.returncode, failure.stderr)
        assert preserved.read_bytes() == b'previous customer PDF', 'Invalid section break damaged original PDF'
    assert not run(executable, original, preserved, '--overwrite', cwd=work)
    assert preserved.read_bytes() == actual.read_bytes(), 'Native recovery diverged from original report'
    return {
        'pages': 3, 'sourceSha256': sha(original), 'oracleSha256': sha(control),
        'pdfSha256': sha(actual), 'nativePdfByteParity': True,
        'verifiedSearchablePages': True, 'protectedFailureRecovery': True
    }


def verify_native_word_break(executable, output, work):
    """Prove bounded glyph-level report wrapping with real native PDF and explicit controls."""
    source = ROOT / 'examples/report-word-break-identifiers.html'
    oracle = ROOT / 'examples/report-word-break-identifiers-oracle.html'
    assert source.is_file() and oracle.is_file(), 'Missing authored word-break report or control'
    actual = output / 'word-break-report.pdf'
    expected = output / 'word-break-report-control.pdf'
    assert not run(executable, source, actual, cwd=work)
    assert not run(executable, oracle, expected, cwd=work)
    run('qpdf', '--check', actual)
    run('qpdf', '--check', expected)
    assert actual.read_bytes() == expected.read_bytes(), (
        'Native break-all PDF glyph layout differs from manual break oracle')
    pages = count_pages(actual)
    assert pages >= 2, '48 identifier examples must paginate'
    placed = [page_boxes(actual, n) for n in range(1, pages + 1)]
    labels = [word[0] for _, _, words in placed for word in words]
    records = [word for word in labels if re.fullmatch(r'REC-\d{2}', word)]
    assert records == [f'REC-{n:02d}' for n in range(1, 49)], (
        'Report IDs missing, duplicated or reordered', records)
    for token in ('PAID', 'EXA', 'MPLE'):
        assert labels.count(token) == 48, ('Word-break glyph group missing or duplicated', token)
    original_positions = [page_boxes(expected, n) for n in range(1, pages + 1)]
    assert placed == original_positions, 'Native source/control glyph positions differ'
    invalid = work / 'word-break-invalid.html'
    retained = output / 'word-break-retained.pdf'
    for html, code in (
        ('<p style="word-break:keep-all">BAD</p>', b'FPDF1202'),
        ('<span style="word-break:break-all">BAD</span>', b'FPDF1201'),
        ('<p style="width:5pt;white-space:nowrap;word-break:break-all">BAD</p>', b'FPDF1302'),
    ):
        invalid.write_text(html, encoding='utf-8')
        retained.write_bytes(b'previous customer PDF')
        failed = subprocess.run([str(executable), str(invalid), str(retained), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code in failed.stderr and not failed.stdout, (
            'Invalid native word-break input was accepted', failed.returncode, failed.stderr)
        assert retained.read_bytes() == b'previous customer PDF', (
            'Rejected native word-break input damaged customer output')
    assert not run(executable, source, retained, '--overwrite', cwd=work)
    assert retained.read_bytes() == actual.read_bytes(), 'Native word-break recovery changed PDF bytes'
    return {'records': 48, 'pages': pages, 'sourceSha256': sha(source),
            'oracleSha256': sha(oracle), 'pdfSha256': sha(actual),
            'nativePdfByteParity': True, 'popplerGlyphPositions': True,
            'protectedFailureRecovery': True}


def verify_native_image_height_limits(executable, output, work, logo):
    """Compare 36 real image-bound PDF records with a separate physical-width control."""
    source = ROOT / 'examples/report-image-height-limits.html'
    oracle = ROOT / 'examples/report-image-height-limits-oracle.html'
    actual = output / 'report-image-height-limits.pdf'
    control = output / 'report-image-height-limits-control.pdf'
    for html, pdf in ((source, actual), (oracle, control)):
        assert not run(executable, html, pdf, '--image', f'logo={logo}', cwd=work)
        run('qpdf', '--check', pdf)
    assert actual.read_bytes() == control.read_bytes(), (
        'Bounded CSS image min/max-height differs from physical PDF image matrix oracle')
    pages = count_pages(actual)
    assert pages >= 2, '36 image height-bound report records must paginate'
    words = []
    for page in range(1, pages + 1):
        width, height, placed = page_boxes(actual, page)
        assert all(0 <= x0 < x1 <= width and 0 <= y0 < y1 <= height
                   for _, x0, y0, x1, y1 in placed), 'Image height-bound text outside page'
        words.extend(label for label, *_ in placed)
    records = [token for token in words if re.fullmatch(r'REC-\d{2}', token)]
    assert records == [f'REC-{n:02d}' for n in range(1, 37)], (
        'Missing, duplicated or reordered height-bound records', records)
    assert words.count('Inspection') == 36, 'Height-bound report lost searchable text'
    assert len(re.findall(rb'/Im\d+ Do', actual.read_bytes())) == 36, (
        'Height-bound images missing or resource duplicated unexpectedly')
    invalid = work / 'image-height-limit-invalid.html'
    retained = output / 'image-height-limit-preserved.pdf'
    for markup, code in (
        ('<img src=logo style="max-height:50%">', b'FPDF1202'),
        ('<img src=logo style="min-height:2rem">', b'FPDF1201'),
        ('<img src=logo style="max-height:0">', b'FPDF1302'),
        ('<img src=logo style="min-height:999pt">', b'FPDF1302'),
        ('<section style="max-height:40pt">Invalid</section>', b'FPDF1201'),
    ):
        invalid.write_text(markup, encoding='utf-8')
        retained.write_bytes(b'previous customer PDF')
        failed = subprocess.run([str(executable), str(invalid), str(retained),
                                 '--overwrite', '--image', f'logo={logo}'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code in failed.stderr and not failed.stdout, (
            'Invalid image height limit was accepted', markup, failed.returncode, failed.stderr)
        assert retained.read_bytes() == b'previous customer PDF', (
            'Rejected height constraint damaged customer PDF')
    assert not run(executable, source, retained, '--overwrite', '--image', f'logo={logo}', cwd=work)
    assert retained.read_bytes() == actual.read_bytes(), 'Height-bound retry PDF differs'
    return {'records': 36, 'pages': pages, 'pdfSha256': sha(actual),
            'sourceSha256': sha(source), 'oracleSha256': sha(oracle),
            'nativePdfByteParity': True, 'protectedFailureRecovery': True}


def verify_native_image_heights(executable, output, work, logo):
    """Measure actual Native AOT PDF image height against a separately authored width oracle."""
    source = ROOT / 'examples/report-image-heights.html'
    oracle = ROOT / 'examples/report-image-heights-oracle.html'
    assert source.is_file() and oracle.is_file(), 'Missing height image source or oracle'
    actual = output / 'report-image-heights.pdf'
    control = output / 'report-image-heights-oracle.pdf'
    for html, pdf in ((source, actual), (oracle, control)):
        assert not run(executable, html, pdf, '--image', f'logo={logo}', cwd=work)
        run('qpdf', '--check', pdf)
    assert actual.read_bytes() == control.read_bytes(), (
        'Image CSS height does not agree with independent physical-width PDF geometry')
    pages = count_pages(actual)
    assert pages >= 2, 'Height image report should paginate'
    words = []
    for page in range(1, pages + 1):
        width, height, located = page_boxes(actual, page)
        assert all(0 <= x0 < x1 <= width and 0 <= y0 < y1 <= height
                   for _, x0, y0, x1, y1 in located), 'Image height report text outside page'
        words.extend(word for word, *_ in located)
    records = [word for word in words if re.fullmatch(r'REC-\d{2}', word)]
    assert records == [f'REC-{n:02d}' for n in range(1, 37)], (
        'Missing, duplicated or reordered image height report record', records)
    assert words.count('Inspection') == 36, 'Image height report lost inspected text'
    assert len(re.findall(rb'/Im\d+ Do', actual.read_bytes())) == 36, (
        'Image height report must draw each explicitly bound resource once per record')
    invalid = work / 'image-height-invalid.html'
    retained = output / 'image-height-retained.pdf'
    for markup, code in (
        ('<img src=logo style="height:50%">', b'FPDF1202'),
        ('<img src=logo style="height:1rem">', b'FPDF1201'),
        ('<img src=logo style="height:0">', b'FPDF1302'),
        ('<img src=logo style="height:999pt">', b'FPDF1302'),
    ):
        invalid.write_text(markup, encoding='utf-8')
        retained.write_bytes(b'previous customer PDF')
        failed = subprocess.run([str(executable), str(invalid), str(retained),
                                 '--overwrite', '--image', f'logo={logo}'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code in failed.stderr and not failed.stdout, (
            'Invalid image height accepted', markup, failed.returncode, failed.stderr)
        assert retained.read_bytes() == b'previous customer PDF', (
            'Invalid image height overwrote retained PDF')
    assert not run(executable, source, retained, '--overwrite', '--image', f'logo={logo}', cwd=work)
    assert retained.read_bytes() == actual.read_bytes(), 'Valid retry differs from source PDF'
    return {'records': 36, 'pages': pages, 'pdfSha256': sha(actual),
            'sourceSha256': sha(source), 'oracleSha256': sha(oracle),
            'nativePdfByteParity': True, 'protectedFailureRecovery': True}


def verify_native_responsive_images(executable, output, work, logo):
    """Compare actual PDF image matrices, positions and page breaks to explicit point-width control."""
    source = ROOT / 'examples/report-responsive-images.html'
    oracle = ROOT / 'examples/report-responsive-images-oracle.html'
    assert source.is_file() and oracle.is_file(), 'Missing responsive image templates'
    actual = output / 'responsive-images.pdf'
    control = output / 'responsive-images-oracle.pdf'
    for html, pdf in ((source, actual), (oracle, control)):
        assert not run(executable, html, pdf, '--image', f'logo={logo}', cwd=work)
        run('qpdf', '--check', pdf)
    assert actual.read_bytes() == control.read_bytes(), (
        'Bounded image min/max/border-box geometry differs from explicit PDF-width control')
    pages = count_pages(actual)
    assert pages >= 2, '36 records must paginate'
    all_words = []
    for page in range(1, pages + 1):
        width, height, words = page_boxes(actual, page)
        assert words, ('No independently extractable text on image report page', page)
        assert all(0 <= x0 < x1 <= width and 0 <= y0 < y1 <= height
                   for _, x0, y0, x1, y1 in words), 'PDF text outside the page'
        all_words.extend(word for word, *_ in words)
    records = [word for word in all_words if re.fullmatch(r'REC-\d{2}', word)]
    assert records == [f'REC-{n:02d}' for n in range(1, 37)], (
        'Responsive report records missing, reordered or duplicated', records)
    assert all_words.count('Inspection') == 36, 'A report row was lost'
    assert len(re.findall(rb'/Im\d+ Do', actual.read_bytes())) == 36, (
        'Explicit bound PNG was not placed exactly once per record')
    invalid = work / 'responsive-image-invalid.html'
    retained = output / 'responsive-images-retained.pdf'
    for markup, code in (
        ('<img src=logo style="width:200pt;min-width:900pt">', b'FPDF1302'),
        ('<img src=logo style="max-width:calc(100% - 1pt)">', b'FPDF1202'),
        ('<img src=logo style="max-width:1rem">', b'FPDF1201'),
        ('<img src=logo style="box-sizing:border-box;max-width:4pt;padding:4pt">', b'FPDF1302'),
    ):
        invalid.write_text(markup, encoding='utf-8')
        retained.write_bytes(b'previous customer PDF')
        failed = subprocess.run([str(executable), str(invalid), str(retained),
                                 '--overwrite', '--image', f'logo={logo}'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code in failed.stderr and not failed.stdout, (
            'Invalid native image constraint was accepted', markup, failed.returncode, failed.stderr)
        assert retained.read_bytes() == b'previous customer PDF', (
            'Invalid native image constraint replaced retained output')
    assert not run(executable, source, retained, '--overwrite', '--image', f'logo={logo}', cwd=work)
    assert retained.read_bytes() == actual.read_bytes(), 'Valid native retry changed PDF bytes'
    return {'records': 36, 'pages': pages, 'sourceSha256': sha(source),
            'oracleSha256': sha(oracle), 'pdfSha256': sha(actual),
            'realImagePlacementAndByteParity': True, 'popplerSearchableText': True,
            'protectedFailureRecovery': True}



def verify_native_report_keep_together(executable, output, work):
    """Real native PDF card placement versus separately authored forced-page control."""
    source = ROOT / 'examples/report-keep-together.html'
    oracle = ROOT / 'examples/report-keep-together-oracle.html'
    assert source.is_file() and oracle.is_file(), 'Missing authored approval card pair'
    actual = output / 'keep-together.pdf'
    control = output / 'keep-together-oracle.pdf'
    assert not run(executable, source, actual, cwd=work)
    assert not run(executable, oracle, control, cwd=work)
    run('qpdf', '--check', actual)
    run('qpdf', '--check', control)
    assert actual.read_bytes() == control.read_bytes(), (
        'Native kept-card PDF differs from a separately authored physical page-control oracle')
    assert count_pages(actual) == 2, 'Kept approval card should move to page two'
    first = [w[0] for w in page_boxes(actual, 1)[2]]
    second = [w[0] for w in page_boxes(actual, 2)[2]]
    assert 'PREVIOUS' in first and 'APPROVAL' in first
    assert 'CARD' not in first, 'Approval card was fragmented on first page'
    assert second.count('CARD') == 2 and 'REF-001' in second, (
        'Approval card or following text absent from searchable page two')
    invalid = work / 'keep-together-invalid.html'
    preserved = output / 'keep-together-retained.pdf'
    for html, code in (
        ('<section style="break-inside:avoid"><div><p>NO</p></div></section>', b'FPDF1201'),
        ('<section style="break-inside:left"><p>NO</p></section>', b'FPDF1202'),
    ):
        invalid.write_text(html, encoding='utf-8')
        preserved.write_bytes(b'original customer report')
        failed = subprocess.run([str(executable), str(invalid), str(preserved), '--overwrite'],
                                cwd=work, capture_output=True, timeout=120)
        assert failed.returncode == 3 and code in failed.stderr and not failed.stdout, (
            'Native card failure contract changed', failed.returncode, failed.stderr)
        assert preserved.read_bytes() == b'original customer report', (
            'Native failed card conversion overwrote existing report')
    assert not run(executable, source, preserved, '--overwrite', cwd=work)
    assert preserved.read_bytes() == actual.read_bytes(), (
        'Valid retry changed native PDF after failed card conversion')
    return {'pages': 2, 'sourceSha256': sha(source), 'oracleSha256': sha(oracle),
            'pdfSha256': sha(actual), 'nativePdfByteParity': True,
            'popplerSearchableText': True, 'protectedFailureRecovery': True}



def verify_native_paper_geometry(executable, source, output, work, default_pdf):
    """Real native binary: four MediaBoxes, Poppler positions, streamed bytes and protected recovery."""
    a4 = output / 'paper-a4-explicit.pdf'
    assert not run(executable, source, a4, '--page-size', 'a4', cwd=work)
    assert a4.read_bytes() == default_pdf.read_bytes(), 'Explicit A4 changed the preexisting default PDF'

    # The authored two-section first-document contains an explicit page break.
    # Use the long-standing default source PDF as a separate full-text oracle,
    # never simply treating valid MediaBoxes as evidence of complete content.
    default_pages = count_pages(default_pdf)
    assert default_pages == 2, 'Authored baseline page-break witness changed'
    baseline_words = Counter(word for page in range(1, default_pages + 1)
                             for word, *_ in page_boxes(default_pdf, page)[2])
    assert baseline_words['FactsPDF'] >= 1 and baseline_words['renderer'] >= 1, \
        'The default PDF no longer contains the authored report markers'

    cases = (
        ('a4', True, '841.89 595.28'),
        ('letter', False, '612 792'),
        ('letter', True, '792 612'),
    )
    records = []
    for paper, landscape, bounds in cases:
        suffix = '-landscape' if landscape else '-portrait'
        pdf = output / f'paper-{paper}{suffix}.pdf'
        flags = ['--page-size', paper] + (['--landscape'] if landscape else [])
        assert not run(executable, source, pdf, *flags, cwd=work), 'Paper geometry leaked stdout'
        run('qpdf', '--check', pdf)
        data = pdf.read_bytes()
        pages = count_pages(pdf)
        assert pages == 2, ('Authored page-break witness gained/lost a page', paper, landscape, pages)
        media = f'/MediaBox [0 0 {bounds}]'.encode('ascii')
        assert data.count(media) == pages, ('MediaBox differs across pages', paper, landscape)
        width, height = [float(x) for x in bounds.split()]
        observed_words = Counter()
        for page in range(1, pages + 1):
            measured_width, measured_height, words = page_boxes(pdf, page)
            assert abs(measured_width - width) < 0.02 and abs(measured_height - height) < 0.02, \
                ('Poppler physical dimensions differ', paper, landscape, page, measured_width,
                 measured_height, bounds)
            assert words, ('Resized report has a blank body page', paper, landscape, page)
            for word, x_min, y_min, x_max, y_max in words:
                assert word and 0 <= x_min < x_max <= width + 0.02 and \
                    0 <= y_min < y_max <= height + 0.02, \
                    ('Resized body text clipped/outside physical page',
                     paper, landscape, page, word, x_min, y_min, x_max, y_max, width, height)
                observed_words[word] += 1
        assert observed_words == baseline_words, \
            ('Resized report lost/duplicated authored words', paper, landscape,
             baseline_words - observed_words, observed_words - baseline_words)
        if paper == 'letter' and landscape:
            stdout_pdf = run(executable, source, '-', *flags, cwd=work)
            assert stdout_pdf == data and stdout_pdf.startswith(b'%PDF-1.7'), \
                'Binary stdout differs from output-file path'
        records.append({'paper': paper, 'landscape': landscape, 'points': bounds,
                        'pageCount': pages, 'sha256': sha(pdf),
                        'wordCount': sum(observed_words.values()),
                        'allPagesWithinMediaBox': True, 'allAuthoredWordsPreserved': True})

    # Prove furniture is re-laid out against landscape Letter coordinates on
    # BOTH pages and does not translate, drop or overwrite the body text.
    landscape = output / 'paper-letter-landscape.pdf'
    furnished = output / 'paper-letter-landscape-furniture.pdf'
    furniture_config = ROOT / 'examples/business-page-furniture.json'
    assert furniture_config.is_file(), 'Missing authored six-region furniture fixture'
    assert not run(executable, source, furnished, '--page-size', 'letter',
                   '--landscape', '--page-furniture', furniture_config, cwd=work), \
        'Resized furniture CLI wrote unexpected stdout'
    run('qpdf', '--check', furnished)
    assert count_pages(furnished) == count_pages(landscape) == 2, \
        'Furniture changed resized report page count'
    furnished_pages = []
    for page in (1, 2):
        base = page_boxes(landscape, page)
        decorated = page_boxes(furnished, page)
        assert abs(decorated[0] - 792) < 0.02 and abs(decorated[1] - 612) < 0.02, \
            ('Furniture page has incorrect physical Letter landscape bounds', page)
        # Existing independent checker also verifies exact furniture word
        # phrases, left/right and margin bands, and body glyph positions.
        furnished_pages.append(validate_furniture_words(base, decorated, page))

    preserved = output / 'paper-geometry-preserved.pdf'
    sentinel = b'original approved report'
    preserved.write_bytes(sentinel)
    bad = subprocess.run([str(executable), str(source), str(preserved), '--overwrite',
                          '--page-size', 'tabloid'], cwd=work, capture_output=True, timeout=120)
    assert bad.returncode == 2 and not bad.stdout and b'--page-size' in bad.stderr, \
        'Unsupported paper failed to produce a protected argument error'
    assert preserved.read_bytes() == sentinel, 'Bad paper geometry overwrote the existing PDF'
    assert not list(output.glob('.factspdf-*.tmp')), 'Paper geometry left temporary output'
    assert not run(executable, source, preserved, '--overwrite', '--page-size', 'letter',
                   '--landscape', cwd=work), 'Paper geometry recovery leaked stdout'
    assert preserved.read_bytes() == (output / 'paper-letter-landscape.pdf').read_bytes(), \
        'Recovery after invalid geometry changed PDF bytes'
    return {'a4DefaultByteParity': True, 'paperCases': records, 'nativeMediaBoxes': True,
            'independentPopplerAllPageBoundsAndFullText': True,
            'resizedLetterFurniture': furnished_pages,
            'stdoutFileByteParity': True,
            'invalidArgumentDestinationPreserved': True, 'recoveryByteParity': True}



def verify_native_custom_paper(executable, source, output, work, baseline_pdf):
    """Independent qpdf/Poppler verification of custom physical page sizes on native CLI."""
    baseline = Counter(word for page in range(1, count_pages(baseline_pdf) + 1)
                       for word, *_ in page_boxes(baseline_pdf, page)[2])
    assert count_pages(baseline_pdf) == 2, 'Custom paper witness requires two authored pages'
    cases = (
        ('a5', '148mm', '210mm', 148 * 72 / 25.4, 210 * 72 / 25.4),
        ('legal', '8.5in', '14in', 612, 1008),
    )
    results = []
    for name, width_value, height_value, width, height in cases:
        target = output / f'custom-paper-{name}.pdf'
        flags = ['--page-width', width_value, '--page-height', height_value, '--margin', '12mm']
        assert not run(executable, source, target, *flags, cwd=work), 'Custom paper leaked stdout'
        run('qpdf', '--check', target)
        pages = count_pages(target)
        assert pages == 2, ('Custom page lost its authored pagination', name, pages)
        observed = Counter()
        for page in range(1, pages + 1):
            physical_width, physical_height, words = page_boxes(target, page)
            assert abs(physical_width - width) < 0.02 and abs(physical_height - height) < 0.02, (
                'Poppler custom page size mismatch', name, page, physical_width, physical_height)
            assert words, ('Blank custom report page', name, page)
            margin = 12 * 72 / 25.4
            for word, x1, y1, x2, y2 in words:
                assert word and margin - 1 < x1 < x2 < width - margin + 1 and (
                    margin - 1 < y1 < y2 < height - margin + 1), (
                    'Custom report word clipped or outside physical print area', name, page,
                    word, x1, y1, x2, y2)
                observed[word] += 1
        assert observed == baseline, ('Custom paper lost/duplicated body words',
                                       name, baseline - observed, observed - baseline)
        if name == 'a5':
            streamed = run(executable, source, '-', *flags, cwd=work)
            assert streamed == target.read_bytes() and streamed.startswith(b'%PDF-1.7'), (
                'Custom paper binary stdout differs from file')
        results.append({'format': name, 'widthPt': width, 'heightPt': height,
                        'pages': pages, 'sha256': sha(target), 'allWordsAndBoundsChecked': True})

    # The layout engine must reflow header/footer text on the true custom paper.
    furnished = output / 'custom-paper-legal-furniture.pdf'
    assert not run(executable, source, furnished, '--page-width', '8.5in',
                   '--page-height', '14in', '--margin', '36pt',
                   '--header-text', 'Internal report', '--footer-text', 'Internal',
                   '--page-numbers', cwd=work), 'Custom page furniture leaked stdout'
    run('qpdf', '--check', furnished)
    assert count_pages(furnished) == 2, 'Custom physical furniture changed page count'
    for page in (1, 2):
        width, height, positioned = page_boxes(furnished, page)
        assert abs(width - 612) < 0.02 and abs(height - 1008) < 0.02
        plain = Counter(word for word, *_ in page_boxes(baseline_pdf, page)[2])
        seen = Counter(word for word, *_ in positioned)
        assert not plain - seen, ('Custom furniture lost body content', page, plain - seen)
        lines = page_lines(furnished, page)
        assert any('Internal report' in line for line in lines), ('Missing custom header', page)
        assert any(f'Internal | {page}' in line for line in lines), ('Missing custom footer', page)
        for word, x1, y1, x2, y2 in positioned:
            assert word and 0 <= x1 < x2 <= width and 0 <= y1 < y2 <= height, (
                'Custom paper furniture outside physical page', page, word)

    # Rejections must not destroy previously approved PDFs, and a retry must work.
    destination = output / 'custom-paper-approved.pdf'
    sentinel = b'previously approved report'
    invalid = (
        ('--page-width', '148mm'),
        ('--page-width', '148mm', '--page-height', '210mm', '--page-size', 'a4'),
        ('--page-width', '148mm', '--page-height', '210mm', '--landscape'),
        ('--page-width', '71pt', '--page-height', '210mm'),
        ('--page-width', '148mm', '--page-height', '210mm',
         '--page-height', '211mm'),
        ('--page-width', '100pt', '--page-height', '210mm', '--margin', '51pt'),
    )
    for flags in invalid:
        destination.write_bytes(sentinel)
        bad = subprocess.run([str(executable), str(source), str(destination), '--overwrite', *flags],
                             cwd=work, capture_output=True, timeout=120)
        assert bad.returncode == 2 and not bad.stdout and bad.stderr, (
            'Invalid custom physical page accepted', flags, bad.returncode, bad.stderr)
        assert destination.read_bytes() == sentinel, ('Invalid page changed saved PDF', flags)
        assert not list(output.glob('.factspdf-*.tmp')), 'Invalid custom page leaked temporary file'
    assert not run(executable, source, destination, '--overwrite', '--page-width',
                   '148mm', '--page-height', '210mm', '--margin', '12mm', cwd=work)
    assert destination.read_bytes() == (output / 'custom-paper-a5.pdf').read_bytes(), (
        'Custom paper retry differs after rejected commands')
    return {'cases': results, 'physicalBodyAndFurniture': True,
            'binaryStdoutParity': True, 'invalidRecovery': True}


def verify_native_page_margin(executable, source, output, work, baseline_pdf):
    """Real physical trim margins with independent Poppler text coordinates and safe failure recovery."""
    unchanged = output / 'margin-default-explicit.pdf'
    assert not run(executable, source, unchanged, '--margin', '36pt', cwd=work)
    assert unchanged.read_bytes() == baseline_pdf.read_bytes(), 'Explicit default margin changed historical PDF'

    expected_words = Counter(word for page in range(1, count_pages(baseline_pdf) + 1)
                             for word, *_ in page_boxes(baseline_pdf, page)[2])
    baseline_heading = min(x for word, x, *_ in page_boxes(baseline_pdf, 1)[2] if word == 'FactsPDF')
    cases = (
        ('18mm', 18 * 72 / 25.4, 'a4', False, 595.28, 841.89),
        ('0.75in', 54, 'letter', True, 792, 612),
    )
    results = []
    for value, inset, paper, landscape, width, height in cases:
        flags = ['--page-size', paper, '--margin', value] + (['--landscape'] if landscape else [])
        target = output / f'margin-{paper}-{"landscape" if landscape else "portrait"}.pdf'
        assert not run(executable, source, target, *flags, cwd=work), 'Margin output leaked stdout'
        run('qpdf', '--check', target)
        pages = count_pages(target)
        assert pages == 2, ('Authored report page count changed with margin', value, pages)
        observed_words = Counter()
        for page in range(1, pages + 1):
            measured_width, measured_height, words = page_boxes(target, page)
            assert abs(measured_width - width) < 0.02 and abs(measured_height - height) < 0.02, (
                'Margin changed the physical page dimensions', value, page)
            assert words, ('Unexpected blank report page with margin', value, page)
            for word, x1, y1, x2, y2 in words:
                assert (word and inset - 1 < x1 < x2 < width - inset + 1
                        and inset - 1 < y1 < y2 < height - inset + 1), (
                    'Text outside user-selected physical print area', value, page, word, x1, y1, x2, y2)
                observed_words[word] += 1
        assert observed_words == expected_words, (
            'Margin lost or duplicated actual authored words', value, expected_words - observed_words,
            observed_words - expected_words)
        heading = min(x for word, x, *_ in page_boxes(target, 1)[2] if word == 'FactsPDF')
        assert abs((heading - baseline_heading) - (inset - 36)) < 0.1, (
            'Margin did not physically move body glyphs to the new left edge', value,
            baseline_heading, heading, inset)
        if landscape:
            stream_pdf = run(executable, source, '-', *flags, cwd=work)
            assert stream_pdf == target.read_bytes() and stream_pdf.startswith(b'%PDF-1.7'), (
                'Margin stdout bytes differ from file output')
        results.append({'margin': value, 'points': inset, 'paper': paper, 'landscape': landscape,
                        'sha256': sha(target), 'pages': pages, 'wordCount': sum(observed_words.values()),
                        'independentPrintAreaChecked': True, 'allAuthoredWordsPreserved': True})

    dest = output / 'margin-preserved.pdf'
    sentinel = b'existing approved PDF'
    for arguments in (('--margin', '300pt'), ('--margin', '12%'),
                      ('--margin', '10pt', '--margin', '20pt')):
        dest.write_bytes(sentinel)
        bad = subprocess.run([str(executable), str(source), str(dest), '--overwrite', *arguments],
                             cwd=work, capture_output=True, timeout=120)
        assert bad.returncode == 2 and not bad.stdout and bad.stderr, (
            'Invalid margin must fail with argument status, not write PDF', arguments)
        assert dest.read_bytes() == sentinel, ('Invalid margin replaced prior output', arguments)
        assert not list(output.glob('.factspdf-*.tmp')), 'Invalid margin leaked temporary output'
    assert not run(executable, source, dest, '--overwrite', '--page-size', 'letter',
                   '--landscape', '--margin', '0.75in', cwd=work), 'Margin recovery leaked stdout'
    assert dest.read_bytes() == (output / 'margin-letter-landscape.pdf').read_bytes(), (
        'Recovery after invalid margin changed binary PDF')
    return {'defaultPdfByteParity': True, 'cases': results, 'stdoutFileByteParity': True,
            'invalidDestinationPreserved': True, 'recoveryByteParity': True}


def verify_native_pdf_metadata(executable, first, output, work, baseline):
    """Check actual native PDF Info, qpdf integrity and unchanged searchable geometry."""
    titled = output / 'document-metadata.pdf'
    title = 'Quarterly inspection — 澳新'
    author = 'Koala Facts'
    subject = 'Facilities status'
    keywords = 'invoice, inspection'
    assert not run(executable, first, titled, '--title', title, '--author', author,
                   '--subject', subject, '--keywords', keywords, cwd=work)
    info = run('pdfinfo', '-enc', 'UTF-8', titled).decode('utf-8')
    for field, expected in [('Title', title), ('Author', author),
                            ('Subject', subject), ('Keywords', keywords)]:
        found = re.search(rf'^{field}:\s*(.*?)\s*
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
        normalized_fixture_sha = validate_row_fixture(long)
        authored_notes, final_note = expected_note_texts(long.read_text(encoding='utf-8'))
        row_result = validate_continued_row([page_lines(continued, page) for page in (1, 2)], authored_notes, final_note)
        again = output / 'continued-row-repeat.pdf'
        assert not run(isolated, long, again, '--image', f'logo={logo}', cwd=work), 'Repeated row native CLI wrote stdout'
        assert continued.read_bytes() == again.read_bytes(), 'Continued row not deterministic'
        for specimen, stem in ((furnished, 'page-furniture'), (continued, 'continued-row')):
            run('pdftoppm', '-f', '1', '-l', '2', '-png', '-r', '72', specimen, output / stem)
            assert len(list(output.glob(stem + '-*.png'))) == 2, 'Missing native page preview'
        assert not list(output.glob('.factspdf-*.tmp')), 'Leaked CLI temporary output'
        list_markers = verify_native_list_markers(isolated, ROOT, output, work)
        patterned_borders = verify_native_patterned_borders(isolated, output, work)
        multiline_notes = verify_native_multiline_notes(isolated, output, work)
        preserved_spaces = verify_native_preserved_space_notes(isolated, output, work)
        preformatted_audit = verify_native_preformatted_audit(isolated, output, work)
        table_vertical_align = verify_native_table_vertical_align(isolated, output, work)
        decoration_paint = verify_native_decoration_paint(isolated, output, work)
        rounded_panels = verify_native_rounded_panels(isolated, output, work)
        minimum_height = verify_native_minimum_height(isolated, output, work)
        fixed_height = verify_native_fixed_height(isolated, output, work)
        section_breaks = verify_native_section_page_breaks(isolated, output, work)
        kept_cards = verify_native_report_keep_together(isolated, output, work)
        paper_geometry = verify_native_paper_geometry(isolated, first, output, work, plain)
        page_margin = verify_native_page_margin(isolated, first, output, work, plain)
        custom_paper = verify_native_custom_paper(isolated, first, output, work, plain)
        document_metadata = verify_native_pdf_metadata(isolated, first, output, work, plain)
        word_break = verify_native_word_break(isolated, output, work)
        responsive_images = verify_native_responsive_images(isolated, output, work, logo)
        image_heights = verify_native_image_heights(isolated, output, work, logo)
        image_height_limits = verify_native_image_height_limits(isolated, output, work, logo)
        report = {
            'sourceHead': run('git', '-C', ROOT, 'rev-parse', 'HEAD').decode().strip(),
            'platform': __import__('platform').platform(),
            'nativeExecutableSha256': sha(original),
            'firstInputSha256': sha(first), 'configSha256': sha(config),
            'continuedInputSha256': sha(long), 'logoSha256': sha(logo),
            'continuedInputLfSha256': normalized_fixture_sha,
            'furniture': {'sha256': sha(furnished), 'pages': 2, 'wordGeometry': furniture_results,
                          'bodyWordGeometryUnchanged': True, 'leftRightAndMarginPositions': True,
                          'repeatBytesEqual': True, 'invalidConfigDestinationPreserved': True,
                          'failureRecoveryBytesEqual': True},
            'continuedRow': {'sha256': sha(continued), **row_result, 'repeatBytesEqual': True},
            'listMarkers': list_markers,
            'patternedBorders': patterned_borders,
            'multilineNotes': multiline_notes,
            'preservedSpaces': preserved_spaces,
            'preformattedAudit': preformatted_audit,
            'tableVerticalAlign': table_vertical_align,
            'decorationPaint': decoration_paint,
            'roundedPanels': rounded_panels,
            'minimumHeight': minimum_height,
            'fixedHeight': fixed_height,
            'sectionPageBreaks': section_breaks,
            'reportKeepTogether': kept_cards,
            'paperGeometry': paper_geometry,
            'pageMargin': page_margin,
            'customPaper': custom_paper,
            'documentMetadata': document_metadata,
            'wordBreak': word_break,
            'responsiveImages': responsive_images,
            'imageHeights': image_heights,
            'imageHeightLimits': image_height_limits,
            'gates': {'nativeProcess': True, 'independentPoppler': True,
                      'humanReferenceApproval': False, 'published': False}
        }
        (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(report))


if __name__ == '__main__':
    main()
, info, re.MULTILINE)
        assert found and found.group(1) == expected, (field, info)
    run('qpdf', '--check', titled)
    assert count_pages(titled) == count_pages(baseline)
    for page in range(1, count_pages(baseline) + 1):
        assert page_boxes(titled, page) == page_boxes(baseline, page), page
    preserved = output / 'metadata-preserved.pdf'
    preserved.write_bytes(b'approved PDF bytes')
    bad = subprocess.run([str(executable), str(first), str(preserved), '--overwrite',
                          '--title', 'bad\nheader'], cwd=work, capture_output=True, timeout=120)
    assert bad.returncode == 2 and not bad.stdout and bad.stderr
    assert preserved.read_bytes() == b'approved PDF bytes'
    assert not run(executable, first, preserved, '--overwrite', '--title', title,
                   '--author', author, '--subject', subject, '--keywords', keywords, cwd=work)
    assert preserved.read_bytes() == titled.read_bytes()
    return {'sha256': sha(titled), 'pages': count_pages(titled),
            'pdfinfoFieldsVerified': ['Title', 'Author', 'Subject', 'Keywords'],
            'bodyGeometryUnchanged': True, 'qpdfIntegrity': True,
            'badInputPreserved': True, 'recoveryByteIdentical': True}


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
        normalized_fixture_sha = validate_row_fixture(long)
        authored_notes, final_note = expected_note_texts(long.read_text(encoding='utf-8'))
        row_result = validate_continued_row([page_lines(continued, page) for page in (1, 2)], authored_notes, final_note)
        again = output / 'continued-row-repeat.pdf'
        assert not run(isolated, long, again, '--image', f'logo={logo}', cwd=work), 'Repeated row native CLI wrote stdout'
        assert continued.read_bytes() == again.read_bytes(), 'Continued row not deterministic'
        for specimen, stem in ((furnished, 'page-furniture'), (continued, 'continued-row')):
            run('pdftoppm', '-f', '1', '-l', '2', '-png', '-r', '72', specimen, output / stem)
            assert len(list(output.glob(stem + '-*.png'))) == 2, 'Missing native page preview'
        assert not list(output.glob('.factspdf-*.tmp')), 'Leaked CLI temporary output'
        list_markers = verify_native_list_markers(isolated, ROOT, output, work)
        patterned_borders = verify_native_patterned_borders(isolated, output, work)
        multiline_notes = verify_native_multiline_notes(isolated, output, work)
        preserved_spaces = verify_native_preserved_space_notes(isolated, output, work)
        preformatted_audit = verify_native_preformatted_audit(isolated, output, work)
        table_vertical_align = verify_native_table_vertical_align(isolated, output, work)
        decoration_paint = verify_native_decoration_paint(isolated, output, work)
        rounded_panels = verify_native_rounded_panels(isolated, output, work)
        minimum_height = verify_native_minimum_height(isolated, output, work)
        fixed_height = verify_native_fixed_height(isolated, output, work)
        section_breaks = verify_native_section_page_breaks(isolated, output, work)
        kept_cards = verify_native_report_keep_together(isolated, output, work)
        paper_geometry = verify_native_paper_geometry(isolated, first, output, work, plain)
        page_margin = verify_native_page_margin(isolated, first, output, work, plain)
        custom_paper = verify_native_custom_paper(isolated, first, output, work, plain)
        word_break = verify_native_word_break(isolated, output, work)
        responsive_images = verify_native_responsive_images(isolated, output, work, logo)
        image_heights = verify_native_image_heights(isolated, output, work, logo)
        image_height_limits = verify_native_image_height_limits(isolated, output, work, logo)
        report = {
            'sourceHead': run('git', '-C', ROOT, 'rev-parse', 'HEAD').decode().strip(),
            'platform': __import__('platform').platform(),
            'nativeExecutableSha256': sha(original),
            'firstInputSha256': sha(first), 'configSha256': sha(config),
            'continuedInputSha256': sha(long), 'logoSha256': sha(logo),
            'continuedInputLfSha256': normalized_fixture_sha,
            'furniture': {'sha256': sha(furnished), 'pages': 2, 'wordGeometry': furniture_results,
                          'bodyWordGeometryUnchanged': True, 'leftRightAndMarginPositions': True,
                          'repeatBytesEqual': True, 'invalidConfigDestinationPreserved': True,
                          'failureRecoveryBytesEqual': True},
            'continuedRow': {'sha256': sha(continued), **row_result, 'repeatBytesEqual': True},
            'listMarkers': list_markers,
            'patternedBorders': patterned_borders,
            'multilineNotes': multiline_notes,
            'preservedSpaces': preserved_spaces,
            'preformattedAudit': preformatted_audit,
            'tableVerticalAlign': table_vertical_align,
            'decorationPaint': decoration_paint,
            'roundedPanels': rounded_panels,
            'minimumHeight': minimum_height,
            'fixedHeight': fixed_height,
            'sectionPageBreaks': section_breaks,
            'reportKeepTogether': kept_cards,
            'paperGeometry': paper_geometry,
            'pageMargin': page_margin,
            'customPaper': custom_paper,
            'wordBreak': word_break,
            'responsiveImages': responsive_images,
            'imageHeights': image_heights,
            'imageHeightLimits': image_height_limits,
            'gates': {'nativeProcess': True, 'independentPoppler': True,
                      'humanReferenceApproval': False, 'published': False}
        }
        (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(report))


if __name__ == '__main__':
    main()
