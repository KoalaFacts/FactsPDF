"""C2 verification only: authored expectations, real font/geometry checks, API/CLI/AOT parity."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
from fontTools.ttLib import TTFont
from verify_font_styles import run, tool, sha, compact, inspect_fonts


def expected_text():
    parts = ['列表报告 / List report']
    for i in range(1, 13):
        parts.append(f'{i}. C2-{i:03d} 北京团队核对第 {i} 项记录。Beijing team checks record {i} and preserves every report identifier.')
        if i == 3:
            parts.extend(['跨页长项继续记录复核步骤。Continue this long item without repeating its marker.'] * 38)
        if i == 5:
            parts += ['C2-N01 检查附件与来源。Check attachments and sources.',
                      'C2-N02 保留续行缩进和层级。Keep continuation indentation and hierarchy.']
        if i == 8:
            parts += ['1. C2-N03 子步骤从一开始。Nested numbering starts at one.',
                      '2. C2-N04 子步骤继续。Nested numbering continues.']
    parts += ['独立列表重新编号。A separate list restarts numbering.',
              '1. C2-R01 新列表第一项。First item in a separate list.', '2.',
              '3. C2-R03 空项仍占编号。Empty items still count.',
              'C2-END 列表结束，正文恢复原来的内容宽度。End of lists; ordinary text resumes.']
    return ' '.join(parts)


def check_text(text):
    assert compact(text) == compact(expected_text()), 'List text, numbering or record order differs'


def check_geometry(pages, font_path):
    with TTFont(font_path) as font:
        cmap = font.getBestCmap()
        width = lambda value: sum(font['hmtx'][cmap[ord(c)]][0] for c in value) * 12 / font['head'].unitsPerEm
        gutter = max(24, max(width(f'{n}.') for n in range(1, 13)) + 6)
        expected_x = 36 + 9 + gutter
        markers = []
        records = {}
        for page_index, words in enumerate(pages):
            assert words, 'Unexpected empty page'
            for word in words:
                assert 35.99 <= word['xMin'] < word['xMax'] <= 559.30, 'Word outside horizontal margins'
                assert 35.99 <= word['yMin'] < word['yMax'] <= 805.91, 'Word outside vertical margins'
                value = word['text']
                if re.fullmatch(r'\d+\.', value):
                    markers.append((page_index, word))
                if re.fullmatch(r'C2-(?:\d{3}|N0[1-4]|R0[13]|END)', value):
                    assert value not in records, 'Duplicate record ID'
                    records[value] = (page_index, word)
        sequence = [str(n) + '.' for n in range(1, 9)] + ['1.', '2.'] + ['9.', '10.', '11.', '12.', '1.', '2.', '3.']
        assert [w['text'] for _, w in markers] == sequence, 'Wrong or repeated list markers'
        for i in range(1, 13):
            page, word = records[f'C2-{i:03d}']
            assert abs(word['xMin'] - expected_x) < .02, 'Inconsistent top-level continuation gutter'
            same_line = [m for p, m in markers if p == page and abs(m['yMin'] - word['yMin']) < .02 and m['text'] == f'{i}.']
            assert len(same_line) == 1, 'Marker detached from first body line'
            marker = same_line[0]
            assert abs(marker['xMin'] + width(marker['text']) - (expected_x - 6)) < .02, 'Marker right edge or gap differs'
        assert records['C2-003'][0] + 1 < records['C2-004'][0], 'Long item did not naturally span pages'
        assert records['C2-010'][0] > records['C2-009'][0], 'Explicit item paragraph page break absent'
        for name in ['C2-N01', 'C2-N02', 'C2-N03', 'C2-N04']:
            assert abs(records[name][1]['xMin'] - (expected_x + 24)) < .02, 'Nested list gutter differs'
        assert abs(records['C2-END'][1]['xMin'] - 36) < .02, 'Ordinary text did not recover its containing width'
    return {'markerCount': len(markers), 'recordCount': len(records), 'topLevelBodyX': expected_x,
            'longItemStartPage': records['C2-003'][0] + 1, 'nextItemStartPage': records['C2-004'][0] + 1}


def inspect(pdf, manifest):
    run(tool('qpdf'), '--check', pdf)
    text = run(tool('pdftotext'), '-raw', pdf, '-').decode()
    check_text(text)
    bbox = run(tool('pdftotext'), '-bbox', pdf, '-').decode()
    root = ET.fromstring(bbox); ns = '{http://www.w3.org/1999/xhtml}'
    pages = [[dict(text=w.text or '', **{k: float(v) for k, v in w.attrib.items()})
              for w in p.iter(ns + 'word')] for p in root.iter(ns + 'page')]
    # Page count was observed and all five original pages opened after first rendering.
    assert len(pages) == 5, 'Reviewed C2 report should have five pages with these pinned fonts'
    geometry = check_geometry(pages, manifest['roles']['normal'][0])
    fonts = inspect_fonts(pdf, manifest, True, {'text': expected_text(), 'phraseRoles': [('normal', 'C2-010')]})
    assert {f['role'] for f in fonts} == {'normal'}
    data = pdf.read_bytes()
    content_ids = re.findall(rb'/Type /Page /Parent[^\n]+/Contents (\d+) 0 R', data)
    contents = [run(tool('qpdf'), pdf, f'--show-object={int(oid)}', '--filtered-stream-data') for oid in content_ids]
    assert len(contents) == 5
    assert sum(len(re.findall(rb' c(?:\s|$)', value)) for value in contents) == 8, 'Expected exactly two four-arc discs'
    run(tool('pdftoppm'), '-png', '-r', '100', pdf, pdf.with_suffix(''))
    pdf.with_suffix('.txt').write_text(text)
    pdf.with_suffix('.bbox.html').write_text(bbox)
    return {'pages': len(pages), 'sha256': sha(data), 'bytes': len(data), 'qpdf': 'pass', 'text': 'exact',
            'geometry': geometry, 'fonts': fonts, 'discCount': 2}, pages, text


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--cli-dll', type=Path, required=True)
    parser.add_argument('--api-dll', type=Path, required=True)
    parser.add_argument('--native-cli', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    source = root / 'examples/lists-report.html'
    manifest = json.loads(args.manifest.read_text())
    out = args.output.resolve(); out.mkdir(parents=True, exist_ok=False)
    fonts = manifest['roles']['normal']
    flags = [x for path in fonts for x in ['--font', path]] + ['--subset-fonts']
    api_pdf = out / 'lists-report-api.pdf'
    api = json.loads(run(args.dotnet, args.api_dll, source, api_pdf, args.manifest))
    for name, command in [('cli', [args.dotnet, args.cli_dll]), ('native', [args.native_cli])]:
        run(*command, source, out / f'lists-report-{name}.pdf', *flags)
        assert (out / f'lists-report-{name}.pdf').read_bytes() == api_pdf.read_bytes(), name + ' PDF differs from API'
    inspection, pages, text = inspect(api_pdf, manifest)
    # Deliberate wrong-content/marker/geometry inputs prove the checker rejects plausible regressions.
    mutations = []
    for name, operation in [('missing-id', lambda: check_text(text.replace('C2-004', 'C2-005'))),
                            ('duplicate-marker', lambda: check_text(text + ' 3.'))]:
        try: operation()
        except AssertionError: mutations.append(name)
        else: raise AssertionError('Inspector accepted ' + name)
    moved = json.loads(json.dumps(pages))
    next(w for p in moved for w in p if w['text'] == 'C2-010')['xMin'] += 10
    try: check_geometry(moved, fonts[0])
    except AssertionError: mutations.append('wrong-gutter')
    else: raise AssertionError('Inspector accepted displaced body')
    errors = []
    for mode, command in [('cli', [args.dotnet, args.cli_dll]), ('native', [args.native_cli])]:
        for name, html, code in [('third-level', '<ol><li><ul><li><ol></ol></li></ul></li></ol>', 'FPDF1101'),
                                ('attribute', '<ol start="2"><li>A</li></ol>', 'FPDF1103'),
                                ('gutter', '<ol style="width:10pt"><li>A</li></ol>', 'FPDF1302'),
                                ('marker-style', '<ol><li style="font-weight:bold"><p style="font-weight:normal">A</p></li></ol>', 'FPDF1507')]:
            input_path = out / f'{mode}-{name}.html'; input_path.write_text(html)
            for destination in ['stdout', 'existing', 'new']:
                target = out / f'{mode}-{name}-{destination}.pdf'
                if destination == 'existing': target.write_bytes(b'sentinel')
                result = subprocess.run([str(x) for x in [*command, input_path, '-' if destination == 'stdout' else target,
                    *(['--overwrite'] if destination == 'existing' else []), *flags]], capture_output=True, timeout=60)
                assert result.returncode == 3 and code.encode() in result.stderr and not result.stdout
                assert target.read_bytes() == b'sentinel' if destination == 'existing' else not target.exists()
            errors.append({'mode': mode, 'name': name, 'code': code, 'stdoutExistingNewProtected': True})
        recovery = out / f'{mode}-recovery.pdf'
        run(*command, source, recovery, *flags)
        assert recovery.read_bytes() == api_pdf.read_bytes()
    report = {'sourceSha': run('git', '-C', root, 'rev-parse', 'HEAD').decode().strip(),
              'dirty': bool(run('git', '-C', root, 'status', '--porcelain').strip()), 'inputSha256': sha(source.read_bytes()),
              'fontInputs': [f for f in manifest['fonts'] if f['role'] == 'normal'], 'api': api,
              'inspection': inspection, 'apiCliNativeByteEquality': True, 'cliErrors': errors, 'checkerRejects': mutations,
              'browser': 'pending-protected-CI', 'visualReview': 'pending-final-artifact-review'}
    (out / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'pages': inspection['pages'], 'sha256': inspection['sha256'], 'parity': True,
                      'apiErrors': len(api['errors']), 'cliErrors': len(errors), 'checkerRejects': mutations}))


if __name__ == '__main__':
    main()
