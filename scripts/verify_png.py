"""Inspect real PNG PDFs against authored pixels, independent decoders and raster output."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess

from PIL import Image
from pypdf import PdfReader
from pypdf.generic import ContentStream

if __package__:
    from .png_fixtures import create
else:
    from png_fixtures import create


def run(command):
    completed = subprocess.run([str(x) for x in command], capture_output=True, timeout=120)
    if completed.returncode:
        raise AssertionError(f'Command failed ({completed.returncode}): {command}\n{completed.stderr.decode(errors="replace")}')
    return completed.stdout


def inspect(path, case, *, qpdf=None, raster=True):
    reader = PdfReader(path, strict=True)
    assert len(reader.pages) == 1, 'PNG sample must occupy one page'
    page = reader.pages[0]
    resources = page['/Resources']['/XObject']
    assert len(resources) == 1, 'one bound image should have one visible XObject'
    obj = next(iter(resources.values())).get_object()
    rgba = bytes(case['rgba'])
    expected = bytes(rgba[i] for i in range(0, len(rgba), 4)) if case['gray'] else bytes(
        value for i in range(0, len(rgba), 4) for value in rgba[i:i + 3])
    assert obj['/Subtype'] == '/Image' and obj['/Filter'] == '/FlateDecode'
    assert int(obj['/BitsPerComponent']) == 8
    assert (int(obj['/Width']), int(obj['/Height'])) == (case['width'], case['height'])
    assert obj['/ColorSpace'] == ('/DeviceGray' if case['gray'] else '/DeviceRGB')
    assert obj.get_data() == expected, 'decoded PDF color plane differs from authored pixels'
    if case['alpha']:
        mask = obj['/SMask'].get_object()
        assert mask['/Subtype'] == '/Image' and mask['/ColorSpace'] == '/DeviceGray'
        assert mask['/Filter'] == '/FlateDecode' and int(mask['/BitsPerComponent']) == 8
        assert (int(mask['/Width']), int(mask['/Height'])) == (case['width'], case['height'])
        assert mask.get_data() == rgba[3::4], 'PDF soft mask differs from authored alpha'
    else:
        assert '/SMask' not in obj, 'opaque PNG unexpectedly has a mask'
    operations = ContentStream(page.get_contents(), reader).operations
    draws = [args for args, operator in operations if operator == b'Do']
    assert len(draws) == 1, 'wrong image draw count'
    matrices = [list(map(float, args)) for args, operator in operations if operator == b'cm']
    assert len(matrices) == 1, 'unexpected image transforms'
    width, zero1, zero2, height, x, bottom = matrices[0]
    assert zero1 == zero2 == 0 and abs(width - 264) < .001
    assert abs(height - 136) < .001 and abs(x - 36) < .001
    top = float(page.mediabox.height) - bottom - height
    assert abs(top - 36) < .001, 'image top/orientation differs'
    assert f'PNG-{case["name"]}' in page.extract_text(), 'following text missing'
    result = {'name': case['name'], 'pdfSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
              'colorBytes': len(expected), 'softMask': case['alpha'], 'pixels': case['width'] * case['height']}
    if qpdf:
        run([qpdf, '--check', path])
        result['qpdf'] = 'pass'
    if raster:
        prefix = path.with_suffix('')
        run(['pdftoppm', '-png', '-singlefile', '-r', '144', path, prefix])
        with Image.open(prefix.with_suffix('.png')) as raw:
            rendered = raw.convert('RGB')
            for sy in range(case['height']):
                for sx in range(case['width']):
                    at = (sy * case['width'] + sx) * 4
                    color, alpha = rgba[at:at + 3], rgba[at + 3]
                    expected_pixel = tuple((c * alpha + bg * (255 - alpha) + 127) // 255
                                           for c, bg in zip(color, (32, 64, 96)))
                    pixel = rendered.getpixel((round((x + (sx + .5) * width / case['width']) * 2),
                                               round((top + (sy + .5) * height / case['height']) * 2)))
                    assert max(abs(a - b) for a, b in zip(pixel, expected_pixel)) <= 2, (
                        f'Wrong composited pixel ({sx},{sy}): {pixel} != {expected_pixel}')
        result['raster'] = 'all authored pixel centers pass at 144 DPI'
    return result


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
    report = {'cases': [], 'sourceHead': run(['git', 'rev-parse', 'HEAD']).decode().strip(),
              'dirty': bool(run(['git', 'status', '--porcelain']).strip()), 'referenceApproval': 'pending-user-review'}
    for case in manifest['cases']:
        image_path = fixtures / case['file']
        with Image.open(io.BytesIO(image_path.read_bytes())) as independent:
            expected_rgba = bytes(case['rgba'])
            if case['name'] == 'gray-key':
                # Pillow exposes the original low-bit tRNS key but some versions
                # compare it to already-expanded gray when converting to RGBA.
                # Check its decoded colors; the literal alpha is checked against
                # the actual PDF mask and Poppler pixels below, independently.
                expected_rgb = bytes(v for i in range(0, len(expected_rgba), 4) for v in expected_rgba[i:i + 3])
                assert independent.info['transparency'] == 1
                assert independent.convert('RGB').tobytes() == expected_rgb, 'fixture/Pillow color disagreement'
            else:
                assert independent.convert('RGBA').tobytes() == expected_rgba, 'fixture/Pillow disagreement'
        source = output / (case['name'] + '.html')
        source.write_text('<div style="width:264pt;background-color:#204060">'
                          '<img src="sample" style="width:264pt"></div>'
                          f'<p>PNG-{case["name"]}</p>')
        previous = None
        for entry, command in commands:
            target = output / (case['name'] + '-' + entry + '.pdf')
            run([*command, source, target, '--overwrite', '--image', f'sample={image_path}'])
            result = inspect(target, case, qpdf=args.qpdf)
            if previous is not None:
                assert target.read_bytes() == previous, 'Native and managed PNG bytes differ'
            previous = target.read_bytes()
            report['cases'].append({'entry': entry, **result})

    # A repeated resource must retain a single color object and a single mask.
    repeated = output / 'reuse.html'
    repeated.write_text('<img src="logo" style="width:108pt"><img src="logo" style="width:108pt"><p>REUSE</p>')
    for entry, command in commands:
        target = output / ('reuse-' + entry + '.pdf')
        run([*command, repeated, target, '--overwrite', '--image', f'logo={fixtures / "brand-mark.png"}'])
        reader = PdfReader(target, strict=True)
        objects = reader.pages[0]['/Resources']['/XObject']
        assert len(objects) == 1, 'repeated PNG duplicated its resource'
        obj = next(iter(objects.values())).get_object()
        assert '/SMask' in obj
        draws = [o for o in ContentStream(reader.pages[0].get_contents(), reader).operations if o[1] == b'Do']
        assert len(draws) == 2 and draws[0][0] == draws[1][0], 'reused image references differ'

    # Real pre-output failure/recovery through the public CLI.
    corrupt = bytearray((fixtures / 'rgba.png').read_bytes())
    corrupt[-1] ^= 1
    invalid = fixtures / 'bad-crc.png'
    invalid.write_bytes(corrupt)
    for entry, command in commands:
        target = output / ('preserve-' + entry + '.pdf')
        target.write_bytes(b'KEEP ORIGINAL')
        failed = subprocess.run([str(x) for x in [*command, repeated, target, '--overwrite', '--image', f'logo={invalid}']],
                                capture_output=True, timeout=120)
        assert failed.returncode == 3 and b'FPDF1601' in failed.stderr and not failed.stdout
        assert target.read_bytes() == b'KEEP ORIGINAL', 'bad PNG replaced target'
        run([*command, repeated, target, '--overwrite', '--image', f'logo={fixtures / "brand-mark.png"}'])
        assert target.read_bytes().startswith(b'%PDF-'), 'recovery failed'
        target.unlink()
    report['resourceReuse'] = 'pass'
    report['failureAndRecovery'] = 'pass'

    sample = output / 'png-brand-report.pdf'
    run([*commands[0][1], 'examples/png-brand-report.html', sample, '--overwrite',
         '--image', f'logo={fixtures / "brand-mark.png"}', '--page-numbers',
         '--header-text', 'FactsPDF | Business report', '--footer-text', 'Synthetic example'])
    sample_reader = PdfReader(sample, strict=True)
    assert len(sample_reader.pages) == 1, 'business report should fit one page'
    sample_text = sample_reader.pages[0].extract_text()
    for value in ['FactsPDF', 'SERVICE SUMMARY', 'Report template setup', '900.00']:
        assert value in sample_text, f'Business report missing {value}'
    assert '/SMask' in next(iter(sample_reader.pages[0]['/Resources']['/XObject'].values())).get_object()
    run(['pdftoppm', '-png', '-singlefile', '-r', '144', sample, sample.with_suffix('')])
    report['businessReport'] = {'file': sample.name, 'pages': 1, 'textAndMask': 'pass'}
    (output / 'results.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'cases': len(report['cases']), 'resourceReuse': 'pass', 'failureAndRecovery': 'pass',
                      'businessReport': sample.name, 'output': str(output)}, indent=2))


if __name__ == '__main__':
    main()
