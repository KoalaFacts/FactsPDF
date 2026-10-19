"""E2 authored native-output checks; no browser or printing dependency."""
import argparse
import json
from pathlib import Path
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject
from verify_tables import rectangles, words
from verify_font_styles import run, tool, sha, compact, ROLES


def cases():
    # 769.89pt usable height; 28pt rows: 27 rows per page including header.
    return [dict(name=f'rows-{n}', count=n, header=True, groups=g, lead=0, bilingual=False)
            for n, g in [(10, [10]), (30, [26, 4]), (100, [26, 26, 26, 22])]] + [
        dict(name='tail-30', count=30, header=True, groups=[1, 26, 3], lead=35, bilingual=False),
        dict(name='no-header-100', count=100, header=False, groups=[27, 27, 27, 19], lead=0, bilingual=False),
        dict(name='bilingual-30', count=30, header=True, groups=[26, 4], lead=0, bilingual=True)]


def html(case):
    lead = ("<p style='margin-top:0;margin-bottom:0;font-size:10pt;line-height:2'>"
            + '<br>'.join(['LEAD'] * case['lead']) + '</p>') if case['lead'] else ''
    header = '<thead><tr><th>ID</th><th>DESCRIPTION</th></tr></thead>' if case['header'] else ''
    rows = ''.join(f'<tr><td>R{i:03}</td><td>{"记录" if case["bilingual"] else "Record"}</td></tr>' for i in range(1, case['count']+1))
    return ("<!doctype html><style>td,th{padding:4pt;border:1pt solid #24445a;background-color:#f4f7fa}"
            "th{font-weight:normal;background-color:#dce7ef}</style>" + lead
            + "<table style='width:480pt;font-size:12pt;line-height:1.5'><colgroup><col style='width:80pt'>"
            "<col style='width:400pt'></colgroup>" + header + '<tbody>' + rows + '</tbody></table>')


def inspect(pdf, case, raster=True):
    run(tool('qpdf'), '--check', pdf)
    reader = PdfReader(pdf); page_words, bbox = words(pdf)
    assert len(reader.pages) == len(case['groups']), 'page count'
    next_id = 1
    for index, (page, count) in enumerate(zip(reader.pages, case['groups'])):
        tokens = [w['text'] for w in page_words[index]]
        expected = (['LEAD'] * case['lead'] if index == 0 else [])
        if case['header']: expected += ['ID', 'DESCRIPTION']
        for i in range(next_id, next_id + count): expected += [f'R{i:03}', '记录' if case['bilingual'] else 'Record']
        assert compact(''.join(tokens)) == compact(''.join(expected)), 'page text/order/header'
        # Inspect the actual filled cell rectangles, independently of text extraction.
        cells = [r['rect'] for r in rectangles(page, reader) if r['rect'][2] in (80,400) and abs(r['rect'][3]-28)<.00001]
        top = 36 + (case['lead'] * 20 if index == 0 else 0)
        expected_cells = []
        for row in range(count + int(case['header'])):
            expected_cells += [[36, round(841.89-top-28*row-28,5),80,28], [116,round(841.89-top-28*row-28,5),400,28]]
        assert [[round(v,5) for v in r] for r in cells] == expected_cells, 'whole row cell geometry'
        for word in page_words[index]:
            assert 35.98 <= word['xMin'] < word['xMax'] <= 516.02, 'text horizontal bounds'
            assert 35.98 <= word['yMin'] < word['yMax'] <= 805.91, 'text vertical bounds'
        # Each row's exact text must remain in its own padded cell on this page.
        for row in range(count + int(case['header'])):
            row_top = top + 28*row
            for col, (x, width) in enumerate([(36,80),(116,400)]):
                cell_words = [w for w in page_words[index] if x <= (w['xMin']+w['xMax'])/2 < x+width
                              and row_top <= (w['yMin']+w['yMax'])/2 < row_top+28]
                value = ('ID' if col == 0 else 'DESCRIPTION') if case['header'] and row == 0 else (
                    f'R{next_id+row-int(case["header"]):03}' if col == 0 else ('记录' if case['bilingual'] else 'Record'))
                assert compact(''.join(w['text'] for w in cell_words)) == compact(value), 'cell text'
                assert cell_words and abs(cell_words[0]['xMin']-(x+5))<.02, 'cell text origin'
                assert all(w['xMax'] <= x+width-4.98 for w in cell_words), 'cell text bounds'
        next_id += count
    assert next_id == case['count'] + 1
    if raster: run(tool('pdftoppm'), '-png', '-r', '72', pdf, pdf.with_suffix(''))
    pdf.with_suffix('.bbox.html').write_text(bbox)
    pdf.with_suffix('.txt').write_bytes(run(tool('pdftotext'), '-raw', pdf, '-'))
    return dict(pages=len(reader.pages), sha256=sha(pdf.read_bytes()), bytes=pdf.stat().st_size,
                rowGroups=case['groups'], qpdf='pass', textAndGeometry='pass')


def mutations(pdf, case, out):
    rejected = []
    for mode in ('missing-header', 'displaced-row'):
        reader = PdfReader(pdf); writer = PdfWriter()
        for page in reader.pages: writer.add_page(page)
        page = writer.pages[1]; stream = ContentStream(page.get_contents(), writer)
        changed = False
        id_matrices = 0
        for values, op in stream.operations:
            if mode == 'missing-header' and op == b'Tj' and str(values[0]) == 'ID':
                values[0] = type(values[0])('XX'); changed=True; break
            if mode == 'displaced-row' and op == b'Tm' and abs(float(values[4])-41)<.01:
                id_matrices += 1
                if id_matrices == 2:
                    values[4] = FloatObject(50); changed=True; break
        assert changed
        page.replace_contents(stream); path = out / f'{mode}.pdf'
        with path.open('wb') as target: writer.write(target)
        try: inspect(path, case, raster=False)
        except AssertionError as error: rejected.append(dict(mode=mode, reason=str(error)))
        else: raise AssertionError('Inspector accepted mutation ' + mode)
        path.unlink()
    return rejected


def main():
    parser = argparse.ArgumentParser()
    for name in ('manifest','output','cli-dll','api-dll','native-cli'): parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--dotnet', required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parents[1]
    out=args.output.resolve(); out.mkdir(parents=True, exist_ok=False)
    manifest=json.loads(args.manifest.read_text())
    assert all(sha(Path(f['path']).read_bytes())==f['sha256'] for f in manifest['fonts'])
    flags=[v for role,paths in manifest['roles'].items() for path in paths for v in (ROLES[role],path)]+['--subset-fonts']
    results=[]
    for case in cases():
        source=root/'tests/fixtures/tables-e2'/f'{case["name"]}.html'
        assert source.read_text()==html(case), 'authored input changed'
        api=out/f'{case["name"]}-api.pdf'
        api_result=json.loads(run(args.dotnet,args.api_dll,source,api,args.manifest,*([] if case['bilingual'] else ['courier'])))
        for name, command in [('cli',[args.dotnet,args.cli_dll]),('native',[args.native_cli])]:
            path=out/f'{case["name"]}-{name}.pdf'
            run(*command,source,path,*(flags if case['bilingual'] else []))
            assert path.read_bytes()==api.read_bytes(), 'API/CLI/native byte equality'
        result=dict(name=case['name'], inputSha256=sha(source.read_bytes()), api=api_result,
                    apiCliNativeByteEquality=True, **inspect(api,case))
        if case['name']=='rows-100': result['mutationsRejected']=mutations(api,case,out)
        results.append(result)
    report=dict(sourceHead=run('git','-C',root,'rev-parse','HEAD').decode().strip(),
                dirty=bool(run('git','-C',root,'status','--porcelain').strip()),cases=results,
                fonts=manifest['fonts'],visualReview='pending every page',referenceApproval='pending user review',
                chromePrint='not run; not required for native E2 acceptance')
    (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps([(r['name'],r['pages']) for r in results]))


if __name__=='__main__': main()
