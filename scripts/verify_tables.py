"""E1 functional evidence: authored fixtures, real PDF geometry/fonts, API/CLI/AOT parity."""
import argparse
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject
from verify_font_styles import run, tool, sha, compact, inspect_fonts, ROLES


def rectangles(page, reader):
    color = None
    result = []
    for values, op in ContentStream(page.get_contents(), reader).operations:
        if op == b'rg': color = tuple(float(v) for v in values)
        elif op == b're': result.append({'rect': [float(v) for v in values], 'color': color})
    return result


def words(pdf):
    bbox = run(tool('pdftotext'), '-bbox', pdf, '-').decode()
    root = ET.fromstring(bbox); ns = '{http://www.w3.org/1999/xhtml}'
    return [[dict(text=w.text or '', **{k: float(v) for k, v in w.attrib.items()})
             for w in p.iter(ns + 'word')] for p in root.iter(ns + 'page')], bbox


def inspect_fixture(pdf, expected):
    run(tool('qpdf'), '--check', pdf)
    reader = PdfReader(pdf)
    assert len(reader.pages) == expected['pages'] == 1, 'authored page count'
    page = reader.pages[0]
    expected_rects = []
    top = 36
    decoration = 2 * (expected['cell_padding'] + expected['cell_border'])
    assert [w - decoration for w in expected['column_outer_widths']] == expected['content_widths']
    heights = [max(row) * expected['line_height'] + decoration for row in expected['row_line_counts']]
    assert heights == expected['row_heights'] and sum(heights) == expected['table_height']
    for height in heights:
        x = 36
        for width in expected['column_outer_widths']:
            # Authored one-point separate borders; no collapsing or redistribution.
            expected_rects += [[x, 841.89-top-1, width, 1], [x, 841.89-top-height, width, 1],
                               [x, 841.89-top-height+1, 1, height-2], [x+width-1, 841.89-top-height+1, 1, height-2]]
            x += width
        top += height
    actual = rectangles(page, reader)
    rounded = lambda rects: sorted(tuple(round(v, 5) for v in r) for r in rects)
    assert rounded([r['rect'] for r in actual]) == rounded(expected_rects), 'authored cell border geometry'
    assert all(max(abs(a-b) for a,b in zip(r['color'], (36/255,68/255,90/255))) <= .00051 for r in actual), 'border color (three-decimal PDF channels)'
    extracted = run(tool('pdftotext'), '-raw', pdf, '-').decode()
    assert compact(extracted) == compact('ID Category Description R001 Overview One complete row R002 Detail First line Second line'), 'authored table text'
    positions, matrix = [], None
    for values, op in ContentStream(page.get_contents(), reader).operations:
        if op == b'Tm': matrix = [float(v) for v in values]
        elif op == b'Tj': positions.append((str(values[0]), round(matrix[4],5), round(matrix[5],5)))
    expected_positions = [(text, x, round(841.89-top-5-12.6-line*18, 5)) for top, cells in [
        (36, [('ID',41,0),('Category',121,0),('Description',261,0)]),
        (64, [('R001',41,0),('Overview',121,0),('One complete row',261,0)]),
        (92, [('R002',41,0),('Detail',121,0),('First line',261,0),('Second line',261,1)])]
        for text,x,line in cells]
    assert positions == expected_positions, ('authored text matrices', positions)
    run(tool('pdftoppm'), '-png', '-r', '100', pdf, pdf.with_suffix(''))
    return {'pages': 1, 'bytes': pdf.stat().st_size, 'sha256': sha(pdf.read_bytes()), 'qpdf': 'pass',
            'columnWidths': expected['column_outer_widths'], 'rowHeights': heights, 'height': sum(heights),
            'textAndMatrices': 'authored-exact', 'borderRectangles': len(actual)}


def verify_fixtures(root, out, commands, api=None, manifest=None):
    fixtures = json.loads((root / 'tests/fixtures/tables-e1/manifest.json').read_text())
    assert fixtures['status'] == 'implemented-e1'
    records = []
    for item in fixtures['cases']:
        source = root / 'tests/fixtures/tables-e1' / item['file']
        assert sha(source.read_bytes()) == item['sha256'], 'Authored HTML changed'
        expected = item['expected']; previous = None
        if 'code' not in expected and api:
            path = out / f"{item['id']}-api.pdf"
            result = json.loads(run(*api, source, path, manifest, 'courier'))
            records.append({'id': item['id'], 'entry': 'api', 'api': result, **inspect_fixture(path, expected)})
            previous = path.read_bytes()
        for name, command in commands.items():
            path = out / f"{item['id']}-{name}.pdf"
            if 'code' not in expected:
                run(*command, source, path)
                if previous is not None: assert path.read_bytes() == previous, 'fixture entry parity'
                previous = path.read_bytes()
                records.append({'id': item['id'], 'entry': name, 'inputSha256': item['sha256'], **inspect_fixture(path, expected)})
            else:
                for target in ('stdout', 'existing', 'new'):
                    destination = out / f"{item['id']}-{name}-{target}.pdf"
                    if target == 'existing': destination.write_bytes(b'sentinel')
                    result = subprocess.run([str(v) for v in [*command, source, '-' if target == 'stdout' else destination,
                        *(['--overwrite'] if target == 'existing' else [])]], capture_output=True, timeout=60)
                    assert result.returncode == 3 and expected['code'].encode() in result.stderr and not result.stdout, result.stderr.decode()
                    assert destination.read_bytes() == b'sentinel' if target == 'existing' else not destination.exists()
                    if target == 'existing': destination.unlink() # sentinel is not a PDF deliverable
                records.append({'id': item['id'], 'entry': name, 'inputSha256': item['sha256'],
                                'code': expected['code'], 'stdoutExistingNewProtected': True})
    for name in commands:
        assert (out / f'points-{name}.pdf').read_bytes() == (out / f'percentages-{name}.pdf').read_bytes()
    return records


def inspect_geometry(pdf, case):
    run(tool('qpdf'), '--check', pdf)
    reader = PdfReader(pdf)
    assert len(reader.pages) == case['pages'], 'example page count'
    text = run(tool('pdftotext'), '-raw', pdf, '-').decode()
    assert compact(text) == compact(case['text']), 'example exact text'
    pages, bbox = words(pdf)
    cells = []
    for index, page in enumerate(reader.pages, 1):
        for entry in rectangles(page, reader):
            x, y, width, height = entry['rect']
            if width in case['tracks'] and height >= 13.5:
                cells.append(dict(page=index, x=x, top=float(page.mediabox.height)-y-height,
                                  width=width, height=height, color=entry['color']))
        for word in pages[index-1]:
            assert 35.98 <= word['xMin'] < word['xMax'] <= 559.30, 'page horizontal bounds'
            assert 35.98 <= word['yMin'] < word['yMax'] <= 805.91, 'page vertical bounds'
    assert len(cells) == len(case['rows'])*3, 'missing cell background'
    assert {cell['page'] for cell in cells} == {case['tablePage']}, 'whole table must occupy one page'
    table_top = cells[0]['top']
    if 'tableTop' in case: assert abs(table_top-case['tableTop']) < .01, 'whole table movement origin'
    top = table_top
    for row_index, row in enumerate(case['rows']):
        group = cells[row_index*3:row_index*3+3]
        assert len({round(cell['height'], 3) for cell in group}) == 1, 'cells must fill their row'
        x = case['tableX']
        for col, cell in enumerate(group):
            assert abs(cell['x']-x) < .01 and abs(cell['top']-top) < .01, 'cell track origin'
            assert cell['width'] == case['tracks'][col], 'cell track width'
            cell_words = [w for w in pages[cell['page']-1] if cell['x'] <= (w['xMin']+w['xMax'])/2 < cell['x']+cell['width']
                          and cell['top'] <= (w['yMin']+w['yMax'])/2 < cell['top']+cell['height']]
            assert compact(''.join(w['text'] for w in cell_words)) == compact(row[col]), 'cell exact text'
            assert abs(cell_words[0]['xMin']-(x+6.75)) < .02, 'cell text origin'
            for word in cell_words:
                assert x+6.73 <= word['xMin'] < word['xMax'] <= x+cell['width']-6.73, 'cell content width'
                assert top+.73 <= word['yMin'] < word['yMax'] <= top+cell['height']-.73, 'cell content height'
            x += cell['width']
        top += group[0]['height']
    if case['name'] == 'tables-page-movement':
        assert all(any(w['text'] == f'LEAD-{i:02d}' for w in pages[0]) for i in range(1,25)), 'leading list page'
        assert not any(w['text'].startswith('E1-') for w in pages[0]), 'table split onto previous page'
        assert any(w['text'] == 'AFTER-ONE' and w['yMin'] > top for w in pages[1]), 'following list overlaps table'
    return {'pages': len(reader.pages), 'tablePage': case['tablePage'], 'tableTop': table_top,
            'tracks': case['tracks'], 'rowHeights': [cells[i]['height'] for i in range(0,len(cells),3)],
            'tableHeight': top-table_top, 'exactCellTextAndBounds': True}, cells, text, bbox


def inspect(pdf, case, manifest):
    geometry, cells, text, bbox = inspect_geometry(pdf, case)
    fonts = inspect_fonts(pdf, manifest, True, case)
    assert {font['role'] for font in fonts} == set(ROLES), 'all four explicit roles'
    run(tool('pdftoppm'), '-png', '-r', '100', pdf, pdf.with_suffix(''))
    png = pdf.with_name(f"{pdf.stem}-{case['tablePage']}.png")
    with Image.open(png).convert('RGB') as image:
        for cell in cells:
            pixel = image.getpixel((round((cell['x']+cell['width']-3)*100/72), round((cell['top']+3)*100/72)))
            assert max(abs(actual-round(expected*255)) for actual,expected in zip(pixel, cell['color'])) <= 3, 'actual cell background raster'
    pdf.with_suffix('.txt').write_text(text); pdf.with_suffix('.bbox.html').write_text(bbox)
    return {'sha256': sha(pdf.read_bytes()), 'bytes': pdf.stat().st_size, 'qpdf': 'pass',
            'geometry': geometry, 'fonts': fonts, 'cellBackgroundRaster': 'pass'}


def prove_mutations(pdf, case, out):
    rejected = []
    for mode in ('missing-text', 'displaced-text'):
        reader = PdfReader(pdf); writer = PdfWriter()
        for page in reader.pages: writer.add_page(page)
        page = writer.pages[case['tablePage']-1]
        content = ContentStream(page.get_contents(), writer)
        operations, group, changed = [], None, False
        for values, op in content.operations:
            if op == b'BT': group = []
            if group is None: operations.append((values, op)); continue
            group.append((values, op))
            if op == b'ET':
                target = next((v for v,o in group if o == b'Tm' and abs(float(v[4])-(case['tableX']+6.75)) < .01), None)
                if target is not None and not changed:
                    changed = True
                    if mode == 'missing-text': group = []
                    else: target[4] = FloatObject(float(target[4])+12)
                operations.extend(group); group = None
        assert changed, 'mutation did not affect actual table text'
        content.operations = operations; page.replace_contents(content)
        path = out / f"{case['name']}-{mode}.pdf"
        with path.open('wb') as stream: writer.write(stream)
        try: inspect_geometry(path, case)
        except AssertionError as error:
            assert str(error) in ('example exact text','cell exact text','cell text origin','cell content width'), str(error)
            rejected.append({'mutation': mode, 'reason': str(error)})
        else: raise AssertionError('Inspector accepted actual mutated PDF: '+mode)
        path.unlink()
    return rejected


def main():
    parser = argparse.ArgumentParser()
    for name in ('manifest','output','cli-dll','api-dll','native-cli'): parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--dotnet', required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parents[1]
    out = args.output.resolve(); out.mkdir(parents=True, exist_ok=False)
    manifest = json.loads(args.manifest.read_text())
    assert all(sha(Path(f['path']).read_bytes()) == f['sha256'] for f in manifest['fonts']), 'font fingerprints'
    commands = {'cli': [args.dotnet,args.cli_dll], 'native': [args.native_cli]}
    api = [args.dotnet,args.api_dll]
    fixture_out = out/'fixtures'; fixture_out.mkdir()
    fixtures = verify_fixtures(root, fixture_out, commands, api, args.manifest)
    flags = [v for role,paths in manifest['roles'].items() for path in paths for v in (ROLES[role],path)] + ['--subset-fonts']
    cases = json.loads(run('node',root/'scripts/wasm_probe/table_cases.mjs'))['cases']
    evidence = {'sourceHead': run('git','-C',root,'rev-parse','HEAD').decode().strip(),
                'dirty': bool(run('git','-C',root,'status','--porcelain').strip()), 'fonts': manifest['fonts'],
                'fixtures': fixtures, 'cases': [], 'browser': 'pending-protected-CI',
                'visualReview': 'pending-every-page-review', 'referenceApproval': 'pending-user-review'}
    for case in cases:
        source = root/'examples'/f"{case['name']}.html"; pdf = out/f"{case['name']}-api.pdf"
        api_result = json.loads(run(*api,source,pdf,args.manifest))
        for name,command in commands.items():
            path = out/f"{case['name']}-{name}.pdf"; run(*command,source,path,*flags)
            assert path.read_bytes() == pdf.read_bytes(), 'actual API/CLI/Native PDF parity'
        evidence['cases'].append({'name': case['name'], 'inputSha256': sha(source.read_bytes()), 'api': api_result,
            'apiCliNativeByteEquality': True, **inspect(pdf,case,manifest), 'checkerRejects': prove_mutations(pdf,case,out)})
    (out/'report.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'cases': [(c['name'],c['geometry']['pages']) for c in evidence['cases']],
                      'fixtureResults': len(fixtures), 'report': str(out/'report.json')}))


if __name__ == '__main__': main()
