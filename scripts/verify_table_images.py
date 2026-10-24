"""Inspect actual image-cell PDFs using authored geometry and independent pixels."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject

if __package__:
    from .png_fixtures import create as create_png
else:
    from png_fixtures import create as create_png


def run(command):
    result = subprocess.run([str(x) for x in command], capture_output=True, timeout=120)
    if result.returncode:
        raise AssertionError(f'Command failed ({result.returncode}): {command}\n{result.stderr.decode(errors="replace")}')
    return result.stdout


def create(directory):
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    image = next(x for x in create_png(directory)['cases'] if x['name'] == 'rgba')
    common = ('<style>table{width:300pt;font-size:10pt;line-height:1.2}'
              'td,th{padding:5pt;border:1pt solid #718096}th{font-weight:normal}'
              '.logo{background-color:#204060}img{width:66pt;padding:2pt;border:1pt solid #cc5500}'
              '</style>')
    columns = '<colgroup><col style="width:120pt"><col style="width:180pt"></colgroup>'
    cases = []
    for alignment, x in [('left', 45), ('center', 63), ('right', 81)]:
        background = (243, 234, 215) if alignment == 'center' else (32, 64, 96)
        image_style = ' style="background-color:#f3ead7"' if alignment == 'center' else ''
        html = (common + '<table>' + columns + '<tbody><tr>'
                f'<td class="logo" style="text-align:{alignment}">\n<img src="sample"{image_style}>\n</td>'
                '<td>ACME<br>DETAILS</td></tr></tbody></table>')
        cases.append({'name': alignment, 'html': html, 'pages': 1,
                      'images': [{'page': 0, 'x': x, 'top': 45, 'width': 66, 'height': 34,
                                  'background': list(background)}],
                      'text': [{'page': 0, 'value': 'ACME', 'x': 162, 'baseline': 790.89},
                               {'page': 0, 'value': 'DETAILS', 'x': 162, 'baseline': 778.89}]})
    movement = json.loads(json.dumps(cases[0]))
    movement.update(name='whole-table-move', pages=2)
    movement['html'] = ('<p style="font-size:10pt;line-height:2;margin-top:0;margin-bottom:0">'
                        + '<br>'.join(['PRELUDE'] * 36) + '</p>' + movement['html'])
    for value in movement['images'] + movement['text']:
        value['page'] = 1
    cases.append(movement)
    repeat = (common + '<table>' + columns + '<thead><tr><th class="logo">'
              '<img src="sample"></th><th>BRANDED HEADER</th></tr></thead><tbody>'
              + ''.join(f'<tr style="line-height:2"><td>ROW-{n:03}</td><td>Service item {n:03}</td></tr>'
                        for n in range(1, 46)) + '</tbody></table>')
    cases.append({'name': 'repeated-header', 'html': repeat, 'pages': 3,
                  'images': [{'page': page, 'x': 45, 'top': 45, 'width': 66, 'height': 34,
                              'background': [32, 64, 96]} for page in range(3)],
                  'text': [{'page': page, 'value': 'BRANDED HEADER', 'x': 162, 'baseline': 790.89}
                           for page in range(3)], 'rowsPerPage': [22, 22, 1]})
    manifest = {'image': image, 'cases': cases}
    (directory / 'table-images.json').write_text(json.dumps(manifest, separators=(',', ':')) + '\n')
    return manifest


def inspect(path, case, image, *, qpdf=None, raster=True):
    reader = PdfReader(path, strict=True)
    assert len(reader.pages) == case['pages'], 'wrong table-image page count'
    rgba = bytes(image['rgba'])
    expected_color = bytes(v for i in range(0, len(rgba), 4) for v in rgba[i:i + 3])
    object_ids, mask_ids, rows = set(), set(), []
    checked = 0
    for page_index, page in enumerate(reader.pages):
        assert abs(float(page.mediabox.width) - 595.28) < .001
        assert abs(float(page.mediabox.height) - 841.89) < .001
        operations = ContentStream(page.get_contents(), reader).operations
        placements, texts = [], []
        matrix = None
        text_matrix = None
        for operands, operator in operations:
            if operator == b'cm':
                matrix = list(map(float, operands))
            elif operator == b'Do':
                assert matrix is not None
                width, zero1, zero2, height, x, bottom = matrix
                assert zero1 == zero2 == 0, 'unexpected image rotation or skew'
                placements.append((x, 841.89 - bottom - height, width, height))
                reference = page['/Resources']['/XObject'].raw_get(operands[0])
                obj = reference.get_object()
                object_ids.add(reference.idnum)
                assert obj['/Subtype'] == '/Image' and obj['/ColorSpace'] == '/DeviceRGB'
                assert obj['/Filter'] == '/FlateDecode' and int(obj['/BitsPerComponent']) == 8
                assert (int(obj['/Width']), int(obj['/Height'])) == (33, 17)
                assert obj.get_data() == expected_color, 'table image color samples changed'
                mask_reference = obj.raw_get('/SMask')
                mask_ids.add(mask_reference.idnum)
                mask = mask_reference.get_object()
                assert mask['/Subtype'] == '/Image' and mask['/ColorSpace'] == '/DeviceGray'
                assert int(mask['/BitsPerComponent']) == 8
                assert (int(mask['/Width']), int(mask['/Height'])) == (33, 17)
                assert mask.get_data() == rgba[3::4], 'table image alpha samples changed'
            elif operator == b'Tm':
                text_matrix = list(map(float, operands))
            elif operator == b'Tj':
                assert text_matrix is not None
                text = str(operands[0])
                texts.append((text, text_matrix[4], text_matrix[5]))
                if text.startswith('ROW-'):
                    rows.append((page_index, text))
        expected_images = [x for x in case['images'] if x['page'] == page_index]
        assert len(placements) == len(expected_images), 'missing, duplicated or misplaced image draw'
        for actual, expected in zip(placements, expected_images):
            assert all(abs(a - expected[k]) < .001 for a, k in zip(actual, ('x', 'top', 'width', 'height'))), (
                f'image geometry {actual} differs from authored {expected}')
        for expected in (x for x in case['text'] if x['page'] == page_index):
            found = [(x, y) for text, x, y in texts if text == expected['value']]
            assert len(found) == 1, f'missing/duplicated cell text {expected["value"]}'
            assert abs(found[0][0] - expected['x']) < .001 and abs(found[0][1] - expected['baseline']) < .001
        if raster and expected_images:
            prefix = path.parent / (path.stem + f'-page-{page_index + 1}')
            run(['pdftoppm', '-f', page_index + 1, '-l', page_index + 1,
                 '-png', '-singlefile', '-r', '144', path, prefix])
            with Image.open(prefix.with_suffix('.png')) as raw:
                pixels = raw.convert('RGB')

                def pixel(x, top, expected):
                    actual = pixels.getpixel((round(x * 2), round(top * 2)))
                    assert max(abs(a - b) for a, b in zip(actual, expected)) <= 2, (
                        f'wrong rendered color at {(x, top)}: {actual} != {expected}')

                for expected in expected_images:
                    for sy in range(17):
                        for sx in range(33):
                            at = (sy * 33 + sx) * 4
                            alpha = rgba[at + 3]
                            blended = tuple((c * alpha + bg * (255 - alpha) + 127) // 255
                                            for c, bg in zip(rgba[at:at + 3], expected['background']))
                            pixel(expected['x'] + (sx + .5) * 2, expected['top'] + (sy + .5) * 2, blended)
                            checked += 1
                    # Padding/background and border must remain above the cell background.
                    pixel(expected['x'] - 1, expected['top'] + 10, expected['background'])
                    pixel(expected['x'] - 2.5, expected['top'] + 10, (204, 85, 0))
    assert len(object_ids) == len(mask_ids) == 1, 'repeated header did not reuse its image/mask identity'
    if 'rowsPerPage' in case:
        assert [text for _, text in rows] == [f'ROW-{n:03}' for n in range(1, 46)], 'body row loss/order/duplication'
        assert [sum(index == page for index, _ in rows) for page in range(case['pages'])] == case['rowsPerPage']
    if qpdf:
        run([qpdf, '--check', path])
    return {'name': case['name'], 'pages': len(reader.pages), 'imageDraws': len(case['images']),
            'uniqueImages': len(object_ids), 'uniqueMasks': len(mask_ids), 'rasterPixelCenters': checked,
            'pdfSha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def reject_mutations(source, case, image, directory):
    for name in ['displaced-image', 'missing-header-image']:
        writer = PdfWriter(clone_from=source)
        page = writer.pages[0]
        stream = ContentStream(page.get_contents(), writer)
        if name == 'displaced-image':
            for operands, operator in stream.operations:
                if operator == b'cm':
                    operands[4] = FloatObject(float(operands[4]) + 1)
                    break
        else:
            stream.operations = [(operands, operator) for operands, operator in stream.operations if operator != b'Do']
        page.replace_contents(stream)
        target = directory / (name + '.pdf')
        writer.write(target)
        try:
            inspect(target, case, image, raster=False)
        except AssertionError:
            pass
        else:
            raise AssertionError(f'Independent inspector accepted {name}')
        target.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--cli-dll', type=Path, required=True)
    parser.add_argument('--native-cli', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--qpdf')
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    fixtures = output / 'fixtures'
    manifest = create(fixtures)
    commands = [('managed', [args.dotnet, args.cli_dll.resolve()])]
    if args.native_cli:
        commands.append(('native', [args.native_cli.resolve()]))
    report = {'sourceHead': run(['git', 'rev-parse', 'HEAD']).decode().strip(),
              'dirty': bool(run(['git', 'status', '--porcelain']).strip()),
              'referenceApproval': 'pending-user-review', 'cases': []}
    for case in manifest['cases']:
        source = output / (case['name'] + '.html')
        source.write_text(case['html'])
        previous = None
        for entry, command in commands:
            target = output / (case['name'] + '-' + entry + '.pdf')
            run([*command, source, target, '--overwrite', '--image', f'sample={fixtures / "rgba.png"}'])
            result = inspect(target, case, manifest['image'], qpdf=args.qpdf)
            if previous is not None:
                assert target.read_bytes() == previous, 'Native and managed table-image output differs'
            previous = target.read_bytes()
            report['cases'].append({'entry': entry, **result})
    reject_mutations(output / 'repeated-header-managed.pdf', manifest['cases'][-1], manifest['image'], output)
    report['actualPdfMutationsRejected'] = ['displaced-image', 'missing-header-image']
    invalid = output / 'mixed-content.html'
    invalid.write_text(manifest['cases'][0]['html'].replace('<img src="sample">', '<span><img src="sample"></span>'))
    for entry, command in commands:
        target = output / ('preserve-' + entry + '.pdf')
        target.write_bytes(b'KEEP ORIGINAL')
        result = subprocess.run([str(x) for x in [*command, invalid, target, '--overwrite',
                                '--image', f'sample={fixtures / "rgba.png"}']], capture_output=True, timeout=120)
        assert result.returncode == 3 and b'FPDF1101' in result.stderr and not result.stdout
        assert target.read_bytes() == b'KEEP ORIGINAL', 'invalid cell replaced the existing target'
        run([*command, output / 'left.html', target, '--overwrite', '--image', f'sample={fixtures / "rgba.png"}'])
        inspect(target, manifest['cases'][0], manifest['image'], raster=False)
        target.unlink()
    report['failureAndRecovery'] = 'pass'
    sample = output / 'branded-business-report.pdf'
    run([*commands[0][1], 'examples/branded-business-report.html', sample, '--overwrite',
         '--image', f'logo={fixtures / "brand-mark.png"}', '--page-numbers',
         '--header-text', 'FACTSPDF | CLIENT REPORT', '--footer-text', 'Synthetic capability sample'])
    sample_reader = PdfReader(sample, strict=True)
    assert len(sample_reader.pages) == 1, 'branded report should occupy one page'
    text = sample_reader.pages[0].extract_text()
    for token in ['Example Operations', 'SERVICE REPORT', 'FP-2026-024', '900.00']:
        assert token in text, f'missing branded-report text: {token}'
    assert '/SMask' in next(iter(sample_reader.pages[0]['/Resources']['/XObject'].values())).get_object()
    if args.qpdf:
        run([args.qpdf, '--check', sample])
    run(['pdftoppm', '-png', '-singlefile', '-r', '110', sample, sample.with_suffix('')])
    report['sample'] = {'pdf': sample.name, 'pages': 1, 'pdfSha256': hashlib.sha256(sample.read_bytes()).hexdigest()}
    (output / 'results.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Table images: {len(report["cases"])} actual outputs, geometry/pixels/reuse/pagination and failure recovery passed.')


if __name__ == '__main__':
    main()
