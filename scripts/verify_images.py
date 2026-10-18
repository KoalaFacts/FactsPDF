"""D1 verification: authored expectations, real DCT streams/raster and actual entry parity."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DecodedStreamObject, NameObject, NumberObject


def sha(data):
    return hashlib.sha256(data).hexdigest()


def run(command, **kwargs):
    return subprocess.run([str(v) for v in command], check=True, capture_output=True, timeout=120, **kwargs).stdout


def inspect(path, item, fixtures, raster=True):
    expected = item['expect']
    reader = PdfReader(path)
    assert len(reader.pages) == expected['page_count'], 'wrong page count'
    objects, draws = {}, []
    for page in reader.pages:
        resources = page['/Resources'].get('/XObject', {})
        for name, reference in resources.items():
            obj = reference.get_object()
            assert obj['/Subtype'] == '/Image', 'unexpected XObject'
            objects[reference.idnum] = obj
        matrix, stack = [1, 0, 0, 1, 0, 0], []
        for operands, operator in ContentStream(page.get_contents(), reader).operations:
            if operator == b'q':
                stack.append(matrix.copy())
            elif operator == b'Q':
                matrix = stack.pop()
            elif operator == b'cm':
                # Engine contract emits independent q/cm/Do/Q image placements.
                assert matrix == [1, 0, 0, 1, 0, 0], 'unexpected nested transform'
                matrix = [float(v) for v in operands]
            elif operator == b'Do':
                draws.append((resources[operands[0]].get_object(), matrix.copy(), float(page.mediabox.height)))
    assert len(objects) == expected.get('image_xobject_count', 1), 'wrong image object count'
    assert len(draws) == expected.get('image_draw_count', 1), 'wrong image draw count'
    used_file = item['resources']['gray' if item['id'] == 'D1-gray-natural' else 'chart']
    original = (fixtures / used_file).read_bytes()
    with Image.open(fixtures / used_file) as source:
        size, mode = source.size, source.mode
    for obj in objects.values():
        assert obj._data == original, 'DCT bytes differ'
        assert obj['/Filter'] == '/DCTDecode' and obj['/BitsPerComponent'] == 8
        assert [int(obj['/Width']), int(obj['/Height'])] == list(size)
        assert obj['/ColorSpace'] == ('/DeviceGray' if mode == 'L' else '/DeviceRGB')
        assert obj['/DecodeParms']['/ColorTransform'] == (0 if mode == 'L' else 1)
    for _, matrix, page_height in draws:
        assert matrix[1:3] == [0, 0] and matrix[0] > 0 and matrix[3] > 0, 'orientation/transform differs'
        dimensions = expected.get('image_content_points', [200, 100])
        assert abs(matrix[0] - dimensions[0]) < .001 and abs(matrix[3] - dimensions[1]) < .001, 'wrong content dimensions'
        if 'image_matrix' in expected:
            assert all(abs(a - b) < .001 for a, b in zip(matrix, expected['image_matrix'])), 'wrong draw matrix'
        assert matrix[4] >= 36 and matrix[4] + matrix[0] <= 559.29
        assert matrix[5] >= 36 and matrix[5] + matrix[3] <= page_height - 36 + .001
    text = run(['pdftotext', '-raw', path, '-']).decode()
    if 'text_order' in expected:
        indices = [text.index(token) for token in expected['text_order']]
        assert indices == sorted(indices) and all(text.count(token) == 1 for token in expected['text_order']), 'text order differs'
        words = ET.fromstring(run(['pdftotext', '-bbox', path, '-']))
        boxes = {w.text: w.attrib for w in words.iter() if w.tag.endswith('word')}
        matrix = draws[0][1]; top = draws[0][2] - matrix[5] - matrix[3]
        assert float(boxes['D1-BEFORE']['yMax']) < top, 'preceding text overlaps image'
        assert float(boxes['D1-AFTER']['yMin']) >= top + matrix[3] - .01, 'following text overlaps image'
    run([os.environ.get('FACTSPDF_QPDF', 'qpdf'), '--check', path])
    evidence = {'bytes': path.stat().st_size, 'sha256': sha(path.read_bytes()), 'pages': len(reader.pages),
                'imageObjects': len(objects), 'draws': [d[1] for d in draws], 'jpegSha256': sha(original), 'qpdf': 'pass'}
    if raster:
        prefix = path.with_suffix('')
        run(['pdftoppm', '-png', '-r', '144', path, prefix])
        png = Path(str(prefix) + '-1.png')
        with Image.open(png) as page, Image.open(fixtures / used_file).convert('RGB') as source:
            for _, matrix, page_height in draws:
                width, height, x, top = matrix[0], matrix[3], matrix[4], page_height - matrix[5] - matrix[3]
                for sx, sy in ([(15, 10), (304, 10), (15, 148), (304, 148), (160, 20), (56, 125)] if mode != 'L' else [(8, 16), (55, 16)]):
                    pixel = page.convert('RGB').getpixel((round((x + (sx + .5) / size[0] * width) * 2),
                                                        round((top + (sy + .5) / size[1] * height) * 2)))
                    assert max(abs(a - b) for a, b in zip(pixel, source.getpixel((sx, sy)))) <= 12, 'raster color/orientation differs'
        evidence['raster'] = {'file': png.name, 'sha256': sha(png.read_bytes()), 'dpi': 144, 'cornerArrowAndBarSamples': 'pass'}
    return evidence


def mutant_checks(path, item, fixtures, output):
    results = []
    for mutation in ['missing-draw', 'duplicate-draw', 'wrong-width', 'wrong-stream']:
        reader = PdfReader(path); writer = PdfWriter(); writer.append(reader)
        page = writer.pages[0]; content = ContentStream(page.get_contents(), writer)
        if mutation == 'missing-draw':
            content.operations = [v for v in content.operations if v[1] != b'Do']
        elif mutation == 'duplicate-draw':
            at = next(i for i, v in enumerate(content.operations) if v[1] == b'Do')
            content.operations.insert(at, content.operations[at])
        elif mutation == 'wrong-width':
            operands = next(v[0] for v in content.operations if v[1] == b'cm'); operands[0] = NumberObject(199)
        else:
            obj = next(iter(page['/Resources']['/XObject'].values())).get_object(); obj._data = (fixtures / 'rgb-444.jpg').read_bytes()
        stream = DecodedStreamObject(); stream.set_data(content.get_data()); page[NameObject('/Contents')] = writer._add_object(stream)
        target = output / (mutation + '.pdf')
        with target.open('wb') as f: writer.write(f)
        try: inspect(target, item, fixtures, raster=False)
        except AssertionError as error: results.append({'mutation': mutation, 'rejected': str(error)})
        else: raise AssertionError('Inspector accepted mutant: ' + mutation)
        target.unlink()
    return results


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dotnet', required=True); parser.add_argument('--cli-dll', type=Path, required=True)
    parser.add_argument('--api-dll', type=Path, required=True); parser.add_argument('--native-cli', type=Path, required=True)
    args = parser.parse_args(); output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    fixtures = Path('tests/fixtures/jpeg').resolve(); contract = json.loads((fixtures / 'acceptance-cases.json').read_text())
    font_manifest = json.loads(args.manifest.read_text()); fonts = font_manifest['roles']['normal']
    for font in font_manifest['fonts']:
        if font['path'] in fonts: assert sha(Path(font['path']).read_bytes()) == font['sha256']
    run([args.dotnet, args.api_dll, fixtures, output, args.manifest])
    evidence = {'sourceHead': run(['git', 'rev-parse', 'HEAD']).decode().strip(),
                'dirty': bool(run(['git', 'status', '--porcelain']).strip()), 'api': json.loads((output / 'api-results.json').read_text()),
                'cases': [], 'corpus': [], 'boundaries': [], 'tools': {}}
    for executable in ['pdftoppm', os.environ.get('FACTSPDF_QPDF', 'qpdf')]:
        p = subprocess.run([executable, '--version' if 'qpdf' in executable else '-v'], capture_output=True)
        evidence['tools'][executable] = (p.stdout + p.stderr).decode().splitlines()[0]
    commands = [('managed-cli', [args.dotnet, args.cli_dll]), ('native-cli-linux', [args.native_cli]),
                ('node-cli', ['node', 'scripts/image_node_cli.mjs', args.dotnet, args.cli_dll])]
    for item in contract['cases']:
        html = item.get('html') or (fixtures / item['html_file']).read_text()
        source = output / (item['id'] + '.html'); source.write_text(html)
        options = [v for name, filename in item['resources'].items() for v in ['--image', name + '=' + str(fixtures / filename)]]
        if 'fonts' in item: options += ['--subset-fonts'] + [v for font in fonts for v in ['--font', font]]
        expected = item['expect']; success = 'code' not in expected
        if success:
            api_pdf = output / (item['id'] + '-api.pdf'); inspected = inspect(api_pdf, item, fixtures)
            evidence['cases'].append({'id': item['id'], 'entry': 'managed-api', **inspected})
        for entry, command in commands:
            if entry not in item['applies_to']: continue
            target = output / (item['id'] + '-' + entry + '.pdf')
            target.write_bytes(b'sentinel')
            result = subprocess.run([str(v) for v in command + [source, target, '--overwrite'] + options], capture_output=True, timeout=120)
            if success:
                assert result.returncode == 0, result.stderr.decode()
                assert target.read_bytes() == api_pdf.read_bytes(), 'entry bytes differ: ' + entry
                inspected = inspect(target, item, fixtures)
                evidence['cases'].append({'id': item['id'], 'entry': entry, 'byteEquality': True, **inspected})
            else:
                assert result.returncode == 3 and expected['code'] in result.stderr.decode(), result.stderr.decode()
                assert target.read_bytes() == b'sentinel' and not result.stdout, 'failure changed output'
                target.unlink()
                evidence['cases'].append({'id': item['id'], 'entry': entry, 'code': expected['code'], 'outputUnchanged': True})
    ascii_source = output / 'ascii.html'; ascii_source.write_text('<p>ASCII</p>')
    for case in json.loads((fixtures / 'manifest.json').read_text())['cases']:
        for entry, command in commands:
            result = subprocess.run([str(v) for v in command + [ascii_source, '-', '--image', 'unused=' + str(fixtures / case['file'])]], capture_output=True, timeout=120)
            expected = case['expected_load']
            assert result.returncode == (0 if expected == 'accept' else 3), result.stderr.decode()
            if expected != 'accept': assert not result.stdout and expected in result.stderr.decode()
            evidence['corpus'].append({'file': case['file'], 'entry': entry, 'result': expected})
    oversized = output / 'oversized.bin'
    with oversized.open('wb') as stream: stream.write((fixtures / 'rgb-420.jpg').read_bytes()); stream.truncate(8_388_609)
    for entry, command in commands:
        for name, options, exit_code, code in [('D1-default-image-count', [v for i in range(17) for v in ['--image', f'i{i}={fixtures / "rgb-420.jpg"}']], 2, None),
                                             ('D1-default-byte-cap', ['--image', 'chart=' + str(oversized)], 3, 'FPDF1603')]:
            target = output / 'sentinel.pdf'; target.write_bytes(b'sentinel')
            result = subprocess.run([str(v) for v in command + [ascii_source, target, '--overwrite'] + options], capture_output=True, timeout=120)
            assert result.returncode == exit_code and (code is None or code in result.stderr.decode())
            assert not result.stdout and target.read_bytes() == b'sentinel'; target.unlink()
            evidence['boundaries'].append({'id': name, 'entry': entry, 'exitCode': exit_code, 'code': code, 'outputUnchanged': True})
    oversized.unlink()
    evidence['inspectorMutations'] = mutant_checks(output / 'D1-image-only-api.pdf', contract['cases'][1], fixtures, output)
    evidence['referenceApproval'] = 'pending-user-review'
    (output / 'results.json').write_text(json.dumps(evidence, indent=2))
    print(json.dumps({'success': True, 'cases': len(evidence['cases']), 'corpus': len(evidence['corpus']), 'boundaries': len(evidence['boundaries'])}))


if __name__ == '__main__': main()
