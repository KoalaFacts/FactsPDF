"""Independent C1 glyph/text/geometry checks and CLI/public API/native parity.
Only owned synthetic HTML/PDF/PNG/JSON are evidence; original fonts stay private.
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
from fontTools.ttLib import TTFont

ROLES = {'normal': '--font', 'bold': '--font-bold', 'italic': '--font-italic', 'boldItalic': '--font-bold-italic'}

def run(*args, timeout=120):
    result = subprocess.run([str(a) for a in args], capture_output=True, timeout=timeout)
    if result.returncode:
        raise RuntimeError(f'{args[0]} exited {result.returncode}: ' + result.stderr.decode(errors='replace') + result.stdout.decode(errors='replace'))
    return result.stdout

def tool(name): return os.environ.get('FACTSPDF_' + name.upper(), name)
def sha(data): return hashlib.sha256(data).hexdigest()
def compact(text): return ''.join(text.split())

def inspect_fonts(pdf, manifest, subset, item):
    data = pdf.read_bytes()
    objects = {int(n): body for n, body in re.findall(rb'(\d+) 0 obj\n(.*?)\nendobj', data, re.S)}
    original_by_name = {f['postscript']: f for f in manifest['fonts']}
    result = []; maps_by_object = {}
    for oid, body in objects.items():
        if b'/Subtype /Type0 ' not in body: continue
        base_name = re.search(rb'/BaseFont /([^\s/]+)', body)[1].decode()
        base_name = re.sub(r'^[A-Z]{6}\+', '', base_name)
        source = original_by_name[base_name]
        cid = objects[int(re.search(rb'/DescendantFonts \[(\d+)', body)[1])]
        descriptor = objects[int(re.search(rb'/FontDescriptor (\d+)', cid)[1])]
        file_id = int(re.search(rb'/FontFile2 (\d+)', descriptor)[1])
        embedded = run(tool('qpdf'), pdf, f'--show-object={file_id}', '--filtered-stream-data')
        unicode_id = int(re.search(rb'/ToUnicode (\d+)', body)[1])
        mapping = run(tool('qpdf'), pdf, f'--show-object={unicode_id}', '--filtered-stream-data')
        cmap = {}
        for block in re.findall(rb'beginbfchar\s+(.*?)\s+endbfchar', mapping, re.S):
            for code, utf16 in re.findall(rb'<([0-9A-F]+)>\s+<([0-9A-F]+)>', block):
                cmap[int(code, 16)] = ord(bytes.fromhex(utf16.decode()).decode('utf-16-be'))
        maps_by_object[oid] = (source['role'], cmap)
        gid_id = int(re.search(rb'/CIDToGIDMap (\d+)', cid)[1])
        gids = run(tool('qpdf'), pdf, f'--show-object={gid_id}', '--filtered-stream-data')
        widths = [float(x) for x in re.search(rb'/W \[1 \[([^]]*)\]\]', cid)[1].split()]
        with TTFont(source['path']) as original, TTFont(io.BytesIO(embedded)) as font:
            assert 'glyf' in font and 'fvar' not in font
            assert font['OS/2'].fsType == original['OS/2'].fsType
            assert font['post'].italicAngle == original['post'].italicAngle
            assert font['OS/2'].usWeightClass == original['OS/2'].usWeightClass
            if not subset: assert embedded == Path(source['path']).read_bytes(), 'Full embedding must preserve bytes'
            else: assert set(font.getBestCmap()) == set(cmap.values()), 'Subset Unicode mapping differs'
            old_map = original.getBestCmap(); new_map = font.getBestCmap()
            for code, scalar in cmap.items():
                old, new = old_map[scalar], new_map[scalar]
                assert font.getGlyphID(new) == int.from_bytes(gids[code*2:code*2+2], 'big')
                assert font['hmtx'][new] == original['hmtx'][old], 'Actual advance/side-bearing changed'
                assert abs(widths[code-1] - original['hmtx'][old][0] * 1000 / original['head'].unitsPerEm) < 0.000001
                assert tuple(map(list, original['glyf'][old].getCoordinates(original['glyf']))) == tuple(map(list, font['glyf'][new].getCoordinates(font['glyf']))), 'Actual glyph outline changed'
            result.append({'resourceObject': oid, 'role': source['role'], 'chainOrder': source['order'],
                'sourceSha256': source['sha256'], 'embeddedBytes': len(embedded), 'scalars': len(cmap),
                'postscript': base_name, 'actualOutlinesAndAdvances': 'equal', 'subset': subset})
    assert result, 'No embedded fonts'
    resources = {name.decode(): int(oid) for name, oid in re.findall(rb'/(F[0-9]+) (\d+) 0 R', data)}
    characters = []
    for body in objects.values():
        if b'stream\n' not in body or b'BT /' not in body: continue
        for resource, codes in re.findall(rb'BT /(F[0-9]+) [^\n]*?<([0-9A-F]+)> Tj ET', body):
            resource_id = resources[resource.decode()]
            if resource_id in maps_by_object:
                role, cmap = maps_by_object[resource_id]
                text = ''.join(chr(cmap[int(codes[i:i+4],16)]) for i in range(0,len(codes),4))
            else:
                assert b'/BaseFont /Courier' in objects[resource_id]
                role, text = 'normal', bytes.fromhex(codes.decode()).decode('ascii')
            characters.extend((char, role) for char in text if not char.isspace())
    joined = ''.join(char for char, _ in characters)
    assert joined == compact(item['text']), 'PDF text commands differ from expected content'
    for role, phrase in item['phraseRoles']:
        matches = list(re.finditer(re.escape(compact(phrase)), joined))
        assert matches, 'Expected style phrase absent: ' + phrase
        for match in matches:
            assert all(actual == role for _, actual in characters[match.start():match.end()]), 'Wrong real font role for: ' + phrase

    # Every content text matrix must be a pure translation. No text rendering
    # mode/stroke or horizontal scaling synthesizes slant/weight.
    for body in objects.values():
        if b'stream\n' not in body or b'BT /' not in body: continue
        assert not re.search(rb'\b(?:Tr|Tz)\b', body)
        for matrix in re.findall(rb'([-0-9.]+) ([-0-9.]+) ([-0-9.]+) ([-0-9.]+) [-0-9.]+ [-0-9.]+ Tm', body):
            assert tuple(map(float, matrix)) == (1, 0, 0, 1), 'Synthetic text transform'
    return result

def inspect(pdf, item, manifest, subset):
    run(tool('qpdf'), '--check', pdf)
    text = run(tool('pdftotext'), '-raw', pdf, '-').decode()
    assert compact(text) == compact(item['text']), f"Text mismatch: {item['name']}"
    info = run(tool('pdfinfo'), pdf).decode(); pages = int(re.search(r'^Pages:\s+(\d+)', info, re.M)[1])
    if item['pages'] is not None: assert pages == item['pages'], (item['name'], pages, item['pages'])
    bbox = run(tool('pdftotext'), '-bbox', pdf, '-').decode()
    root = ET.fromstring(bbox); ns = '{http://www.w3.org/1999/xhtml}'
    bounds = []
    for page in root.iter(ns + 'page'):
        words = list(page.iter(ns + 'word'))
        assert words, 'Unexpected blank page'
        for word in words:
            assert 0 <= float(word.attrib['xMin']) < float(word.attrib['xMax']) <= float(page.attrib['width'])
            assert 0 <= float(word.attrib['yMin']) < float(word.attrib['yMax']) <= float(page.attrib['height'])
        bounds.append({k: (min if k.endswith('Min') else max)(float(w.attrib[k]) for w in words) for k in ('xMin','xMax','yMin','yMax')})
    font_evidence = inspect_fonts(pdf, manifest, subset, item)
    expected_roles = {'italic'} if item.get('selection') == 'courier' else {'italic', 'boldItalic'} if item['name'] == 'styles-long' else set(ROLES)
    assert {f['role'] for f in font_evidence} == expected_roles
    run(tool('pdftoppm'), '-png', '-r', '110', pdf, pdf.with_suffix(''))
    pdf.with_suffix('.txt').write_text(text)
    pdf.with_suffix('.bbox.html').write_text(bbox)
    return {'pages': pages, 'bounds': bounds, 'fonts': font_evidence, 'pdfSha256': sha(pdf.read_bytes()), 'pdfBytes': pdf.stat().st_size,
        'actualPhraseFontRoles': 'pass', 'textExactIgnoringLayoutWhitespace': True, 'qpdf': 'pass', 'bbox': bbox}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dotnet', required=True)
    parser.add_argument('--cli-dll', type=Path, required=True)
    parser.add_argument('--api-dll', type=Path, required=True)
    parser.add_argument('--native-cli', type=Path)
    args = parser.parse_args(); manifest = json.loads(args.manifest.read_text())
    fixtures = json.loads(run('node', 'scripts/wasm_probe/style_fixtures.mjs'))
    args.output.mkdir(parents=True, exist_ok=True)
    evidence = {'head': run('git','rev-parse','HEAD').decode().strip(), 'dirty': bool(run('git','status','--porcelain').strip()),
        'fonts': manifest, 'cases': [], 'visualReview': 'pending', 'browser': 'not run by this script'}
    for item in fixtures['cases']:
        input_path = args.output / (item['name']+'.html'); input_path.write_text(item['html'])
        selected = {k: v[:1] if item.get('selection') == 'latin-only' else v for k,v in manifest['roles'].items()
                    if item.get('selection') != 'courier' or k == 'italic'}
        modes = ['subset', 'full'] if item.get('selection') == 'latin-only' else ['subset']
        previous = None
        for mode in modes:
            prefix = args.output / (item['name']+'-'+mode)
            cli = prefix.with_suffix('.pdf'); api = Path(str(prefix)+'-api.pdf')
            font_args = [arg for role,paths in selected.items() for path in paths for arg in (ROLES[role], path)]
            run(args.dotnet, args.cli_dll, input_path, cli, '--overwrite', *font_args, *(['--subset-fonts'] if mode == 'subset' else []))
            api_result = json.loads(run(args.dotnet, args.api_dll, input_path, api, args.manifest, mode, *([item['selection']] if 'selection' in item else [])))
            assert api.read_bytes() == cli.read_bytes(), 'CLI/direct .NET API bytes differ'
            result = inspect(cli, item, manifest, mode == 'subset')
            if previous is not None:
                assert result['bbox'] == previous['bbox'], 'Subset geometry differs'
                subset_image = args.output / (item['name']+'-subset-1.png')
                assert cli.with_name(cli.stem+'-1.png').read_bytes() == subset_image.read_bytes(), 'Full/subset raster differs'
            if args.native_cli:
                native = Path(str(prefix)+'-native.pdf')
                run(args.native_cli, input_path, native, '--overwrite', *font_args, *(['--subset-fonts'] if mode == 'subset' else []))
                assert native.read_bytes() == cli.read_bytes(), 'Native AOT bytes differ'
            result.update(name=item['name'], mode=mode, apiByteEquality=True, nativeByteEquality=True if args.native_cli else None,
                apiRuntimeDynamicCode=api_result['dynamicCodeSupported'])
            previous = result
            evidence['cases'].append({k:v for k,v in result.items() if k != 'bbox'})
    evidence['errors'] = []
    for item in fixtures['errors']:
        input_path = args.output / (item['name']+'.html'); input_path.write_text(item['html'])
        selected = {role: manifest['roles'][role][:1] if item.get('latinOnly') else manifest['roles'][role] for role in item['roles']}
        font_args = [arg for role,paths in selected.items() for path in paths for arg in (ROLES[role], path)]
        for target in ('stdout', 'existing', 'new'):
            destination = args.output / (item['name']+'-'+target+'.pdf')
            if target == 'existing': destination.write_bytes(b'keep')
            elif destination.exists(): destination.unlink()
            proc = subprocess.run([str(args.dotnet), str(args.cli_dll), str(input_path), '-' if target == 'stdout' else str(destination),
                '--overwrite', '--subset-fonts', *font_args], capture_output=True, timeout=120)
            assert proc.returncode == 3 and item['code'].encode() in proc.stderr, proc.stderr
            assert not proc.stdout, 'Failure emitted partial PDF to stdout'
            assert destination.read_bytes() == b'keep' if target == 'existing' else not destination.exists()
            assert not list(args.output.glob('.factspdf-*.tmp')), 'Temporary PDF left after error'
            if target == 'existing': destination.unlink()
        evidence['errors'].append({'name': item['name'], 'code': item['code'], 'stdoutEmpty': True,
            'existingPreserved': True, 'newAbsent': True, 'temporaryFilesAbsent': True})
    # Full original four-CJK embedding exceeds the existing 16 MiB output cap.
    # Keep that boundary; do not enlarge it just to make a sample succeed.
    full_target = args.output/'full-cjk-budget.pdf'; full_target.write_bytes(b'keep')
    all_font_args = [arg for role,paths in manifest['roles'].items() for path in paths for arg in (ROLES[role], path)]
    proc = subprocess.run([str(args.dotnet), str(args.cli_dll), str(args.output/'styles-report.html'), str(full_target),
        '--overwrite', *all_font_args], capture_output=True, timeout=120)
    assert proc.returncode == 3 and b'FPDF1401' in proc.stderr and not proc.stdout
    assert full_target.read_bytes() == b'keep'
    full_target.unlink() # sentinel is not a renderable PDF artifact
    assert not list(args.output.glob('.factspdf-*.tmp'))
    evidence['errors'].append({'name': 'full-cjk-output-budget', 'code': 'FPDF1401', 'existingPreserved': True})
    # Recovery uses the same input/resources after all failure paths.
    recovered = args.output/'recovered.pdf'
    run(args.dotnet, args.cli_dll, args.output/'styles-report.html', recovered, '--overwrite', '--subset-fonts', *all_font_args)
    assert recovered.read_bytes() == (args.output/'styles-report-subset.pdf').read_bytes()
    evidence['recoveryByteEquality'] = True
    (args.output/'report.json').write_text(json.dumps(evidence, indent=2, ensure_ascii=False)+'\n')
    print(json.dumps({'cases': [(r['name'],r['mode'],r['pages']) for r in evidence['cases']], 'report': str(args.output/'report.json')}))

if __name__ == '__main__': main()
