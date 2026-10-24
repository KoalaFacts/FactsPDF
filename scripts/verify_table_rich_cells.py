"""Independent actual-PDF oracle for authored rich/spanning table fixtures.

Expected coordinates and pixels are authored here, never obtained from renderer
internals or from a generated golden PDF. No production modules are imported.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess

from PIL import Image
import PIL
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject

if __package__:
    from .png_fixtures import png, create as create_png
else:
    from png_fixtures import png, create as create_png

PAGE_HEIGHT = 841.89
CELL = [228, 243, 240]
BORDER = [36, 68, 90]
IMAGE_BORDER = [204, 85, 0]
QUADRANTS = [(240, 20, 40, 255), (20, 220, 60, 128),
             (30, 50, 230, 0), (40, 70, 200, 255)]
COMMON = ('<style>table{width:200pt;font-size:10pt;line-height:1.2}'
          'td,th{padding:4pt;border:1pt solid #24445a;background-color:#e4f3f0}'
          'th{font-weight:normal}'
          'img{width:50%;padding:2pt;border:1pt solid #cc5500}</style>')
COLUMNS = ('<colgroup><col style="width:40pt"><col style="width:60pt">'
           '<col style="width:100pt"></colgroup>')


def run(command):
    result = subprocess.run([str(x) for x in command], capture_output=True, timeout=120)
    assert result.returncode == 0, (f'Command failed ({result.returncode}): {command}\n'
                                    + result.stderr.decode(errors='replace'))
    return result.stdout


def table(cell, sibling='Z', *, second='', cell_style=''):
    return (COMMON + '<table>' + COLUMNS + '<tbody><tr><td colspan="2"'
            + (f' style="{cell_style}"' if cell_style else '') + '>' + cell
            + '</td><td>' + sibling + '</td></tr>' + second + '</tbody></table>')


def text(value, x, baseline_top, page=0):
    return {'value': value, 'x': x, 'baseline': PAGE_HEIGHT - baseline_top, 'page': page}


def image(x, top, width=45, height=22.5, *, page=0, background=None,
          padding=2, border=1):
    return {'page': page, 'x': x, 'top': top, 'width': width, 'height': height,
            'background': background or CELL, 'padding': padding, 'border': border}


def witness(x, top, color, page=0):
    return {'page': page, 'x': x, 'top': top, 'color': color}


def create(directory):
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    rgba = bytes(c for y in range(16) for x in range(32)
                 for c in QUADRANTS[(2 if y >= 8 else 0) + (1 if x >= 16 else 0)])
    (directory / 'sample.png').write_bytes(png(32, 16, 6, 8,
        [rgba[y * 128:(y + 1) * 128] for y in range(16)]))
    cases = []

    def add(name, html, texts, *, images=None, height=None, witnesses=None, pages=1, **extra):
        # These edge witnesses discriminate row height from mere text placement.
        marks = list(witnesses or [])
        if height is not None:
            marks += [witness(130, 36 + height - .5, BORDER),
                      witness(130, 36 + height + .5, [255, 255, 255])]
        cases.append({'name': name, 'html': html, 'pages': pages, 'text': texts,
                      'images': images or [], 'witnesses': marks, **extra})

    add('span', table('SPAN', second='<tr><td>A</td><td>B</td><td>C</td></tr>'),
        [text('SPAN', 41, 50), text('Z', 141, 50), text('A', 41, 72),
         text('B', 81, 72), text('C', 141, 72)], height=44,
        witnesses=[witness(76.5, 39, CELL), witness(76.5, 61, BORDER)])
    add('paragraph-margins', table('<p style="margin-top:2pt;margin-bottom:6pt">FIRST</p>'
        '<p style="margin-top:4pt;margin-bottom:3pt">LAST</p>'),
        [text('FIRST', 41, 52), text('LAST', 41, 70), text('Z', 141, 50)], height=45)
    rich = '<p>FIRST</p><img src="sample"><p>LAST</p>'
    add('rich-left', table(rich), [text('FIRST', 41, 50), text('LAST', 41, 98.5),
        text('Z', 141, 50)], images=[image(44, 64)], height=78.5)
    centered = rich.replace('<img src="sample">', '<img src="sample" style="background-color:#f3ead7">')
    add('rich-center', table(centered, cell_style='text-align:center'),
        [text('FIRST', 71, 50), text('LAST', 74, 98.5), text('Z', 141, 50)],
        images=[image(63.5, 64, background=[243, 234, 215])], height=78.5)
    add('percent-bases', table('<img src="sample" style="padding:10%;border:none">',
        cell_style='padding:5%;text-align:center'), [text('Z', 141, 50)],
        images=[image(64, 50.8, 44, 22, padding=8.8, border=0)], height=51.6)
    add('empty-paragraphs', table('<p></p><p> </p>', sibling=''), [], height=18)
    add('bare-whitespace', table(' \n ', sibling=''), [], height=10)
    direct = 'BEFORE<img src="sample"><p>AFTER</p>TAIL'
    add('direct-around-items', table(direct) + '<p style="font-size:10pt">FOLLOWING</p>',
        [text('BEFORE', 41, 50), text('AFTER', 41, 90.5), text('TAIL', 41, 110.5),
         text('Z', 141, 50), text('FOLLOWING', 36, 127.5)],
        images=[image(44, 56)], height=82.5)
    header = ('<thead><tr><th colspan="3"><p style="margin-bottom:4pt">BRAND</p>'
              '<img src="sample" style="width:64pt">'
              '<p style="margin-top:2pt;margin-bottom:0">CAPTION</p></th></tr></thead>')
    repeated = COMMON + '<table>' + COLUMNS + header + '<tbody>' + ''.join(
        f'<tr style="line-height:2"><td>R{n:03}</td><td>X{n:03}</td><td>Y{n:03}</td></tr>'
        for n in range(1, 48)) + '</tbody></table>'
    add('repeated-header', repeated,
        [t for page in range(3) for t in [text('BRAND', 41, 50, page), text('CAPTION', 41, 106, page)]],
        images=[image(44, 60, 64, 32, page=page) for page in range(3)], pages=3,
        witnesses=[witness(76.5, 39, CELL, page) for page in range(3)]
                  + [witness(136.5, 39, CELL, page) for page in range(3)],
        rowsPerPage=[23, 23, 1])
    move = copy.deepcopy(cases[2])
    move.update(name='whole-table-move', pages=2,
        html='<p style="font-size:10pt;line-height:2;margin-top:0;margin-bottom:0">'
             + '<br>'.join(['PRELUDE'] * 35) + '</p>' + cases[2]['html'])
    for value in move['text'] + move['images'] + move['witnesses']:
        value['page'] = 1
    cases.append(move)
    errors = [
        {'name': 'malformed-colspan', 'html': table('X').replace('colspan="2"', 'colspan="+2"'), 'code': 'FPDF1103'},
        {'name': 'coverage-overflow', 'html': table('X').replace('colspan="2"', 'colspan="3"'), 'code': 'FPDF1101'},
        {'name': 'coverage-underfill', 'html': table('X').replace('<td>Z</td>', ''), 'code': 'FPDF1101'},
        {'name': 'overridden-illegal-p', 'html': table('<p style="padding:1pt;padding:initial">X</p>'), 'code': 'FPDF1201'},
        {'name': 'oversized-rich-row', 'html': table('<p>' + '<br>'.join(['LONG'] * 70) + '</p>'), 'code': 'FPDF1302'},
    ]
    manifest = {'source': 'literal authored acceptance geometry; no renderer imports',
                'referenceApproval': 'pending-user-review',
                'image': {'file': 'sample.png', 'width': 32, 'height': 16, 'rgba': list(rgba)},
                'cases': cases, 'errors': errors}
    (directory / 'table-rich.json').write_text(json.dumps(manifest, separators=(',', ':')) + '\n')
    return manifest


def inspect(path, case, source_image, *, qpdf=None, raster=True):
    path = Path(path)
    reader = PdfReader(path, strict=True)
    assert len(reader.pages) == case['pages'], 'wrong rich-table page count'
    rgba = bytes(source_image['rgba'])
    rgb = bytes(v for at in range(0, len(rgba), 4) for v in rgba[at:at + 3])
    image_ids, mask_ids, rows, body_cells = set(), set(), [], []
    checked = 0
    for index, page in enumerate(reader.pages):
        assert abs(float(page.mediabox.width) - 595.28) < .001
        assert abs(float(page.mediabox.height) - PAGE_HEIGHT) < .001
        placements, texts = [], []
        matrix = text_matrix = None
        for operands, operator in ContentStream(page.get_contents(), reader).operations:
            if operator == b'cm':
                matrix = list(map(float, operands))
            elif operator == b'Tm':
                text_matrix = list(map(float, operands))
            elif operator == b'Tj':
                assert text_matrix is not None
                value = str(operands[0])
                texts.append((value, text_matrix[4], text_matrix[5]))
                if len(value) == 4 and value.startswith('R') and value[1:].isdigit():
                    rows.append((index, value, text_matrix[4], text_matrix[5]))
                if len(value) == 4 and value[0] in 'RXY' and value[1:].isdigit():
                    body_cells.append((index, value, text_matrix[4], text_matrix[5]))
            elif operator == b'Do':
                assert matrix is not None
                width, a, b, height, x, bottom = matrix
                assert a == b == 0, 'unexpected image rotation/skew'
                placements.append((x, PAGE_HEIGHT - bottom - height, width, height))
                reference = page['/Resources']['/XObject'].raw_get(operands[0])
                obj = reference.get_object()
                image_ids.add(reference.idnum)
                assert obj['/Subtype'] == '/Image' and obj['/ColorSpace'] == '/DeviceRGB'
                assert obj['/Filter'] == '/FlateDecode' and int(obj['/BitsPerComponent']) == 8
                assert (int(obj['/Width']), int(obj['/Height'])) == (32, 16)
                assert obj.get_data() == rgb, 'authored RGB samples changed'
                reference = obj.raw_get('/SMask')
                mask_ids.add(reference.idnum)
                mask = reference.get_object()
                assert mask['/ColorSpace'] == '/DeviceGray' and int(mask['/BitsPerComponent']) == 8
                assert (int(mask['/Width']), int(mask['/Height'])) == (32, 16)
                assert mask.get_data() == rgba[3::4], 'authored alpha samples changed'
        expected_images = [v for v in case['images'] if v['page'] == index]
        assert len(placements) == len(expected_images), 'missing/duplicated image draw'
        for actual, expected in zip(placements, expected_images):
            assert all(abs(a - expected[k]) < .001 for a, k in zip(actual, ('x', 'top', 'width', 'height'))), (
                f'image geometry {actual} differs from authored {expected}')
        expected_text = [v for v in case['text'] if v['page'] == index]
        for expected in expected_text:
            found = [(x, y) for value, x, y in texts if value == expected['value']]
            assert len(found) == 1, f'missing/duplicated rich text {expected["value"]}'
            assert abs(found[0][0] - expected['x']) < .001 and abs(found[0][1] - expected['baseline']) < .001, (
                f'rich text geometry {expected["value"]}: {found[0]} != {expected}')
        # No phantom text commands in the all-empty fixtures.
        if case['name'] in ('empty-paragraphs', 'bare-whitespace'):
            assert not texts, 'empty cell created a phantom glyph command'
        ordered = [v for v, _, _ in texts if v in [e['value'] for e in expected_text] and v != 'Z']
        assert ordered == [e['value'] for e in expected_text if e['value'] != 'Z'], 'rich text order changed'
        if raster:
            prefix = path.parent / (path.stem + f'-page-{index + 1}')
            run(['pdftoppm', '-f', index + 1, '-l', index + 1, '-png', '-singlefile',
                 '-r', '288', path, prefix])
            with Image.open(prefix.with_suffix('.png')) as raw:
                pixels = raw.convert('RGB')

                def pixel(x, top, color, reason):
                    nonlocal checked
                    actual = pixels.getpixel((int(x * 4), int(top * 4)))
                    assert max(abs(a - b) for a, b in zip(actual, color)) <= 2, (
                        f'{reason}: wrong raster color at {(x, top)}: {actual} != {color}')
                    checked += 1

                for expected in expected_images:
                    for sy in range(16):
                        for sx in range(32):
                            at = (sy * 32 + sx) * 4
                            alpha = rgba[at + 3]
                            color = [(c * alpha + bg * (255 - alpha) + 127) // 255
                                     for c, bg in zip(rgba[at:at + 3], expected['background'])]
                            pixel(expected['x'] + (sx + .5) * expected['width'] / 32,
                                  expected['top'] + (sy + .5) * expected['height'] / 16,
                                  color, 'source pixel center')
                    pixel(expected['x'] - expected['padding'] / 2,
                          expected['top'] + expected['height'] / 2,
                          expected['background'], 'image padding')
                    if expected['border']:
                        pixel(expected['x'] - expected['padding'] - .5,
                              expected['top'] + expected['height'] / 2,
                              IMAGE_BORDER, 'image border')
                    pixel(38, expected['top'] + expected['height'] / 2, CELL, 'cell padding')
                for mark in case['witnesses']:
                    if mark['page'] == index:
                        pixel(mark['x'], mark['top'], mark['color'], 'span/row boundary')
    assert len(image_ids) == len(mask_ids) == (1 if case['images'] else 0), 'image/mask identity reuse changed'
    if 'rowsPerPage' in case:
        assert [v for _, v, _, _ in rows] == [f'R{n:03}' for n in range(1, 48)], 'body row loss/order/duplication'
        assert [sum(p == i for p, *_ in rows) for i in range(3)] == case['rowsPerPage'], 'whole-row pagination changed'
        # 36 + 78 header + 5 edges + (9 + (20-12)/2) baseline = 132.
        expected_cells = []
        row_number = 1
        for page, count in enumerate(case['rowsPerPage']):
            for n in range(count):
                expected_cells.extend((page, f'{prefix}{row_number:03}', x, PAGE_HEIGHT - 132 - n * 30)
                                      for prefix, x in [('R', 41), ('X', 81), ('Y', 141)])
                row_number += 1
        assert [(p, v) for p, v, _, _ in body_cells] == [(p, v) for p, v, _, _ in expected_cells], 'body sibling cell loss/order/duplication'
        for actual, expected in zip(body_cells, expected_cells):
            assert abs(actual[2] - expected[2]) < .001 and abs(actual[3] - expected[3]) < .001, 'body cell geometry changed'
    if qpdf:
        run([qpdf, '--check', path])
    return {'name': case['name'], 'pages': len(reader.pages), 'imageDraws': len(case['images']),
            'uniqueImages': len(image_ids), 'uniqueMasks': len(mask_ids),
            'rasterPixelCentersAndWitnesses': checked, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def reject_mutations(source, case, source_image, directory):
    evidence = []
    # Establish that serialization by this independent library is accepted first.
    control = directory / 'rewrite-control.pdf'
    PdfWriter(clone_from=source).write(control)
    inspect(control, case, source_image)
    evidence.append({'name': 'pypdf-rewrite-control', 'accepted': True})
    for name, intended in [('missing-rich-text', 'missing/duplicated rich text FIRST'),
                           ('false-span-border', 'span/row boundary: wrong raster color')]:
        writer = PdfWriter(clone_from=source)
        page = writer.pages[0]
        stream = ContentStream(page.get_contents(), writer)
        if name == 'missing-rich-text':
            stream.operations = [(a, o) for a, o in stream.operations if not (o == b'Tj' and str(a[0]) == 'FIRST')]
        else:
            # Add an actual colored rectangle inside the authored colspan.
            stream.operations += [([], b'q'), ([FloatObject(v) for v in (36/255, 68/255, 90/255)], b'rg'),
                                  ([FloatObject(v) for v in (76, PAGE_HEIGHT - 56, 1, 20)], b're'),
                                  ([], b'f'), ([], b'Q')]
        page.replace_contents(stream)
        target = directory / (name + '.pdf')
        writer.write(target)
        try:
            inspect(target, case, source_image)
        except AssertionError as error:
            assert intended in str(error), f'{name} failed for an unrelated assertion: {error}'
            evidence.append({'name': name, 'rejectedBy': str(error)})
        else:
            raise AssertionError(f'Oracle accepted actual-PDF mutation {name}')
    return evidence


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--cli-dll', type=Path, required=True)
    parser.add_argument('--native-cli', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--qpdf')
    parser.add_argument('--baseline', type=Path, help='Optional captured local before/after manifest')
    parser.add_argument('--example', type=Path, help='Optional business-table-rich-cells.html sample inspection')
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    fixtures = output / 'fixtures'
    manifest = create(fixtures)
    image_arg = f'sample={fixtures / "sample.png"}'
    commands = [('managed', [args.dotnet, args.cli_dll.resolve()])]
    if args.native_cli:
        commands.append(('native', [args.native_cli.resolve()]))
    report = {'sourceHead': run(['git', 'rev-parse', 'HEAD']).decode().strip(),
              'dirty': bool(run(['git', 'status', '--porcelain']).strip()),
              'cliSha256': hashlib.sha256(args.cli_dll.read_bytes()).hexdigest(),
              'engineSha256': hashlib.sha256((args.cli_dll.parent / 'FactsPDF.dll').read_bytes()).hexdigest(),
              'nativeSha256': hashlib.sha256(args.native_cli.read_bytes()).hexdigest() if args.native_cli else None,
              'tools': {'dotnet': run([args.dotnet, '--version']).decode().strip(),
                        'pypdf': pypdf.__version__, 'Pillow': PIL.__version__,
                        'raster': 'Poppler pdftoppm, 288dpi', 'qpdf': args.qpdf},
              'referenceApproval': 'pending-user-review', 'cases': [], 'failures': []}
    try:
        for case in manifest['cases']:
            source = output / (case['name'] + '.html')
            source.write_text(case['html'])
            previous = None
            for entry, command in commands:
                target = output / (case['name'] + '-' + entry + '.pdf')
                run([*command, source, target, '--overwrite', '--image', image_arg])
                evidence = inspect(target, case, manifest['image'], qpdf=args.qpdf)
                if previous is not None:
                    assert target.read_bytes() == previous, 'Native/managed rich-table bytes differ'
                previous = target.read_bytes()
                report['cases'].append({'entry': entry, **evidence})
        # Add the literal no-internal-border witness to a rich case for mutation testing.
        mutation_case = copy.deepcopy(manifest['cases'][2])
        mutation_case['witnesses'].append(witness(76.5, 39, CELL))
        report['mutations'] = reject_mutations(output / 'rich-left-managed.pdf', mutation_case, manifest['image'], output)
        for item in manifest['errors']:
            source = output / (item['name'] + '.html')
            source.write_text(item['html'])
            for entry, command in commands:
                for existing in (False, True):
                    target = output / (f'failure-{entry}-{existing}.pdf')
                    target.unlink(missing_ok=True)
                    if existing:
                        target.write_bytes(b'KEEP ORIGINAL')
                    failed = subprocess.run([str(v) for v in [*command, source, target, '--overwrite', '--image', image_arg]],
                                            capture_output=True, timeout=120)
                    assert failed.returncode == 3 and item['code'].encode() in failed.stderr and not failed.stdout, (
                        f'wrong diagnostic for {item["name"]}: {failed.stderr!r}')
                    assert (target.read_bytes() == b'KEEP ORIGINAL') if existing else not target.exists(), 'failure wrote destination'
                    run([*command, output / 'rich-left.html', target, '--overwrite', '--image', image_arg])
                    assert target.read_bytes() == (output / f'rich-left-{entry}.pdf').read_bytes(), 'recovery bytes changed'
                    report['failures'].append({'name': item['name'], 'entry': entry, 'code': item['code'],
                                               'existing': existing, 'preserved': True, 'recoveryByteEquality': True})
        if args.baseline:
            baseline = json.loads(args.baseline.read_text())
            root = args.baseline.parent.resolve()
            report['legacyBaseline'] = []
            for item in baseline['cases']:
                target = output / ('legacy-' + item['name'] + '.pdf')
                image_flags = ['--image', f'sample={root / item["image"]}'] if item['image'] else []
                run([*commands[0][1], root / item['html'], target, '--overwrite', *image_flags])
                compared = target.read_bytes()
                assert compared == (root / item['pdf']).read_bytes(), f'legacy bytes changed: {item["name"]}'
                digest = hashlib.sha256(compared).hexdigest()
                assert digest == item['sha256'], f'captured baseline hash changed: {item["name"]}'
                report['legacyBaseline'].append({'name': item['name'], 'byteEquality': True, 'sha256': digest})
        if args.example:
            assets = fixtures / 'example-assets'
            create_png(assets)
            target = output / 'business-table-rich-cells.pdf'
            run([*commands[0][1], args.example.resolve(), target, '--overwrite',
                 '--image', f'logo={assets / "brand-mark.png"}'])
            sample = PdfReader(target, strict=True)
            assert len(sample.pages) == 1, 'business rich-cell example should be one page'
            sample_text = sample.pages[0].extract_text()
            for token in ['Example Operations', 'DETAILED SERVICE REPORT', 'FP-2026-025',
                          'Brand asset with its own border and caption.', '900.00', 'Full-width note:']:
                assert token in sample_text, f'missing example token: {token}'
            sample_images = sample.pages[0]['/Resources']['/XObject']
            image_names = [a[0] for a, o in ContentStream(sample.pages[0].get_contents(), sample).operations if o == b'Do']
            assert len(image_names) == 2, 'example should draw logo in header and rich cell'
            assert len({sample_images.raw_get(name).idnum for name in image_names}) == 1, 'example logo identity not shared'
            if args.qpdf:
                run([args.qpdf, '--check', target])
            run(['pdftoppm', '-png', '-singlefile', '-r', '110', target, target.with_suffix('')])
            report['example'] = {'pages': 1, 'imageDraws': 2, 'uniqueImages': 1,
                                 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
        report['status'] = 'passed' 
    except Exception as error:
        report['status'] = 'failed'
        report['failure'] = str(error)
        raise
    finally:
        (output / 'results.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Rich tables: {len(report["cases"])} actual outputs; geometry, pixels, mutations and recovery passed.')


if __name__ == '__main__':
    main()
