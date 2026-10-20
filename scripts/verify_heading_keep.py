"""F1 native-engine evidence: authored pages, qpdf/text/bounds and entry parity."""
import argparse
import json
from pathlib import Path
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject
from verify_tables import words
from verify_font_styles import run, tool, sha, compact, ROLES

CSS = "<style>p,h2{font-size:12pt;line-height:2;margin-top:0;margin-bottom:0;font-weight:normal}</style>"


def cases():
    def lead(n): return '<p>' + '<br>'.join(['LEAD']*n) + '</p>'
    def heading(text='TITLE', extra=''): return f"<h2 style='break-after:avoid;{extra}'>{text}</h2>"
    base = lead(30)
    return [
        ('tail', CSS+base+heading()+'<p>P1<br>P2</p>', [['LEAD']*30, ['TITLE','P1','P2']], False),
        ('fits', CSS+lead(28)+heading('TI<span>TLE</span>')+'<p>P1<br>P2</p>', [['LEAD']*28+['TITLE','P1','P2']], False),
        ('overflow', CSS+base+heading()+'<p>'+'<br>'.join(f'P{i}' for i in range(1,41))+'</p>',
         [['LEAD']*30+['TITLE','P1'], [f'P{i}' for i in range(2,34)], [f'P{i}' for i in range(34,41)]], False),
        ('chain-empty', CSS+base+heading('ONE')+'<p> </p>'+heading('TWO')+'<p><br></p><p>P1</p>',
         [['LEAD']*30, ['ONE','TWO','P1']], False),
        ('explicit', CSS+base+heading()+"<p style='break-before:page'>P1<br>P2</p>",
         [['LEAD']*30+['TITLE'], ['P1','P2']], False),
        ('decorated', CSS+base+heading(extra='padding:2pt;border:1pt solid #24445a;margin-bottom:3pt;background-color:#dce7ef')
         +"<p style='padding:2pt;border:1pt solid #24445a;margin-top:5pt'>P1<br>P2</p>", [['LEAD']*30, ['TITLE','P1','P2']], False),
        ('ancestor', CSS+base+"<div style='padding:5pt;border:1pt solid #24445a'>"+heading(extra='margin-top:8pt')+'<p>P1<br>P2</p></div>',
         [['LEAD']*30, ['TITLE','P1','P2']], False),
        ('bilingual', CSS+base+heading('标题 <strong>Bold</strong> <em>Italic</em>')+'<p>正文第一行<br>Second line</p>',
         [['LEAD']*30, ['标题','Bold','Italic','正文第一行','Second','line']], True)]


def inspect(pdf, expected, raster=True):
    run(tool('qpdf'), '--check', pdf)
    reader=PdfReader(pdf); page_words, bbox=words(pdf)
    assert len(reader.pages)==len(expected), 'page count'
    for index, values in enumerate(expected):
        actual=page_words[index]
        assert compact(''.join(w['text'] for w in actual))==compact(''.join(values)), 'page text/order'
        assert all(35.98 <= w['xMin'] < w['xMax'] <= 559.3 and 35.98 <= w['yMin'] < w['yMax'] <= 805.91 for w in actual), 'text bounds'
    if raster:
        run(tool('pdftoppm'), '-png', '-r', '72', pdf, pdf.with_suffix(''))
        pdf.with_suffix('.bbox.html').write_text(bbox)
        pdf.with_suffix('.txt').write_bytes(run(tool('pdftotext'), '-raw', pdf, '-'))
    return dict(pages=len(expected), sha256=sha(pdf.read_bytes()), bytes=pdf.stat().st_size, qpdf='pass', textAndBounds='pass')


def reject_mutations(pdf, expected, out):
    rejected=[]
    for mode in ('missing-title', 'displaced-title'):
        reader=PdfReader(pdf); writer=PdfWriter()
        for page in reader.pages: writer.add_page(page)
        page=writer.pages[1]; stream=ContentStream(page.get_contents(), writer)
        changed=False
        for values, op in stream.operations:
            if mode=='missing-title' and op==b'Tj' and str(values[0])=='TITLE':
                values[0]=type(values[0])('XXXXX'); changed=True; break
            if mode=='displaced-title' and op==b'Tm':
                values[4]=FloatObject(0); changed=True; break
        assert changed
        page.replace_contents(stream); path=out/f'{mode}.pdf'
        with path.open('wb') as f: writer.write(f)
        try: inspect(path, expected, False)
        except AssertionError as error: rejected.append(dict(mode=mode, reason=str(error)))
        else: raise AssertionError('Inspector accepted mutation '+mode)
        path.unlink()
    return rejected


def main():
    parser=argparse.ArgumentParser()
    for name in ('manifest','output','cli-dll','api-dll','native-cli'): parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--dotnet',required=True)
    args=parser.parse_args(); root=Path(__file__).resolve().parents[1]
    out=args.output.resolve(); out.mkdir(parents=True,exist_ok=False)
    manifest=json.loads(args.manifest.read_text())
    assert all(sha(Path(f['path']).read_bytes())==f['sha256'] for f in manifest['fonts'])
    flags=[v for role,paths in manifest['roles'].items() for path in paths for v in (ROLES[role],path)]+['--subset-fonts']
    results=[]
    for name,html,expected,bilingual in cases():
        source=out/f'{name}.html'; source.write_text(html)
        api=out/f'{name}-api.pdf'
        runtime=json.loads(run(args.dotnet,args.api_dll,source,api,args.manifest,'subset',*(() if bilingual else ('courier',))))
        for entry, command in [('cli',[args.dotnet,args.cli_dll]),('native',[args.native_cli])]:
            path=out/f'{name}-{entry}.pdf'
            run(*command,source,path,*(flags if bilingual else []))
            assert path.read_bytes()==api.read_bytes(), 'entry byte equality'
        result=dict(name=name,inputSha256=sha(source.read_bytes()),api=runtime,apiCliNativeByteEquality=True,**inspect(api,expected))
        if name=='fits':
            plain=out/'fits-auto.html'; plain.write_text(html.replace('break-after:avoid;',''))
            auto=out/'fits-auto.pdf'; run(args.native_cli,plain,auto)
            assert auto.read_bytes()==api.read_bytes(), 'fits-here unchanged bytes'
            result['fitsAutoByteEquality']=True
        if name=='tail': result['mutationsRejected']=reject_mutations(api,expected,out)
        results.append(result)
    report=dict(sourceHead=run('git','-C',root,'rev-parse','HEAD').decode().strip(),
                dirty=bool(run('git','-C',root,'status','--porcelain').strip()),cases=results,
                fonts=manifest['fonts'],visualReview='pending every page',chromePrint='not run; native F1 acceptance')
    (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps([(r['name'],r['pages']) for r in results]))


if __name__=='__main__': main()
