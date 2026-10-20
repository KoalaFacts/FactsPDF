"""F2 native decimal page numbers: entry parity, independent text/geometry and defaults."""
import argparse
import json
from pathlib import Path
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject
from verify_tables import words
from verify_font_styles import run, tool, sha, ROLES
from verify_heading_keep import cases as heading_cases
from verify_table_pagination import cases as table_cases, html as table_html


def cases():
    heading={c[0]:c[1] for c in heading_cases()}
    def explicit(n): return ''.join(f"<p style='break-before:{'auto' if i==1 else 'page'};margin-top:0;margin-bottom:0'>PAGE</p>" for i in range(1,n+1))
    return [
        dict(name='empty',html='',pages=1,fonts=False),
        dict(name='single',html='<p>A</p>',pages=1,fonts=False),
        dict(name='explicit-three',html=explicit(3)+"<p style='break-after:page'></p>",pages=3,fonts=False),
        dict(name='heading-tail',html=heading['tail'],pages=2,fonts=False),
        dict(name='table-thirty',html=table_html(table_cases()[1]),pages=2,fonts=False),
        dict(name='bilingual',html=heading['bilingual'],pages=2,fonts=True),
        dict(name='latin-full',html='<p><strong>Bold</strong> <em>Italic</em></p>',pages=1,fonts=True,full=True),
        dict(name='counter-100',html=explicit(100),pages=100,fonts=False)]


def inspect(pdf, plain, expected, raster=True):
    run(tool('qpdf'),'--check',pdf)
    reader=PdfReader(pdf); old=PdfReader(plain)
    assert len(reader.pages)==len(old.pages)==expected, 'page count'
    actual,bbox=words(pdf); original,_=words(plain)
    footers=[]
    for index,(page,items,body) in enumerate(zip(reader.pages,actual,original)):
        assert len(items)==len(body)+1, 'one number per page'
        footer=items[-1]
        assert footer['text']==str(index+1), 'sequential page number'
        assert items[:-1]==body, 'body text/position changed'
        width=float(page.mediabox.width);height=float(page.mediabox.height)
        assert abs((footer['xMin']+footer['xMax'])/2-width/2)<.02, 'horizontal center'
        assert height-36+1.98 <= footer['yMin'] < footer['yMax'] <= height-1.98, 'footer margin bounds'
        assert all(w['yMax']+1.98 <= footer['yMin'] for w in body), 'body overlap'
        footers.append(footer)
    if raster:
        # The 100-page transition corpus has identical authored body content.
        # Inspect all footer strips plus full pages at both digit transitions.
        if expected==100:
            run(tool('pdftoppm'),'-png','-r','72','-y','790','-H','52',pdf,pdf.with_suffix(''))
            for page in [1,9,10,99,100]:
                run(tool('pdftoppm'),'-png','-r','72','-f',page,'-l',page,'-singlefile',pdf,pdf.with_name(f'{pdf.stem}-full-{page}'))
        else: run(tool('pdftoppm'),'-png','-r','72',pdf,pdf.with_suffix(''))
        pdf.with_suffix('.bbox.html').write_text(bbox)
        pdf.with_suffix('.txt').write_bytes(run(tool('pdftotext'),'-raw',pdf,'-'))
    return dict(pages=expected,sha256=sha(pdf.read_bytes()),bytes=pdf.stat().st_size,footerBounds=footers,
                qpdf='pass',textOrderAndGeometry='pass',bodyUnchanged=True)


def reject_mutations(pdf,plain,out):
    rejected=[]
    for mode in ['wrong-number','displaced-number']:
        reader=PdfReader(pdf);writer=PdfWriter()
        for page in reader.pages: writer.add_page(page)
        page=writer.pages[1];stream=ContentStream(page.get_contents(),writer)
        changed=False
        for values,op in reversed(stream.operations):
            if mode=='wrong-number' and op==b'Tj': values[0]=type(values[0])('9');changed=True;break
            if mode=='displaced-number' and op==b'Tm': values[5]=FloatObject(100);changed=True;break
        assert changed
        page.replace_contents(stream);path=out/f'{mode}.pdf'
        with path.open('wb') as f: writer.write(f)
        try: inspect(path,plain,3,False)
        except AssertionError as e: rejected.append(dict(mode=mode,reason=str(e)))
        else: raise AssertionError('Accepted mutation '+mode)
        path.unlink()
    return rejected


def main():
    p=argparse.ArgumentParser()
    for name in ['manifest','output','cli-dll','api-dll','native-cli']:p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--dotnet',required=True);p.add_argument('--baseline-cli',type=Path)
    args=p.parse_args();root=Path(__file__).resolve().parents[1];out=args.output.resolve();out.mkdir(parents=True,exist_ok=False)
    manifest=json.loads(args.manifest.read_text())
    assert all(sha(Path(f['path']).read_bytes())==f['sha256'] for f in manifest['fonts'])
    font_flags=[v for role,paths in manifest['roles'].items() for path in paths for v in (ROLES[role],path)]
    results=[]
    for case in cases():
        name=case['name'];source=out/f'{name}.html';source.write_text(case['html'])
        mode='full' if case.get('full') else 'subset';selection='fonts' if case['fonts'] else 'courier'
        flags=(font_flags if case['fonts'] else [])+([] if mode=='full' else ['--subset-fonts'])
        api=out/f'{name}-api.pdf';plain=out/f'{name}-off-api.pdf'
        runtime=json.loads(run(args.dotnet,args.api_dll,source,api,args.manifest,selection,mode,'on'))
        run(args.dotnet,args.api_dll,source,plain,args.manifest,selection,mode,'off')
        for entry,command in [('cli',[args.dotnet,args.cli_dll]),('native',[args.native_cli])]:
            path=out/f'{name}-{entry}.pdf';run(*command,source,path,*flags,'--page-numbers')
            assert path.read_bytes()==api.read_bytes(),'API/CLI/native bytes'
            off=out/f'{name}-off-{entry}.pdf';run(*command,source,off,*flags)
            assert off.read_bytes()==plain.read_bytes(),'default API/CLI/native bytes'
        result=dict(name=name,inputSha256=sha(source.read_bytes()),api=runtime,apiCliNativeByteEquality=True,
                    defaultApiCliNativeByteEquality=True,**inspect(api,plain,case['pages']))
        if args.baseline_cli:
            baseline=out/f'{name}-baseline.pdf';run(args.baseline_cli,source,baseline,*flags)
            assert baseline.read_bytes()==plain.read_bytes(),'default differs from baseline'
            result['baselineByteEquality']=True
        if name=='explicit-three':result['mutationsRejected']=reject_mutations(api,plain,out)
        results.append(result)
    report=dict(sourceHead=run('git','-C',root,'rev-parse','HEAD').decode().strip(),dirty=bool(run('git','-C',root,'status','--porcelain').strip()),
                cases=results,fonts=manifest['fonts'],visualReview='pending',chromePrint='not run locally; native engine evidence')
    if args.baseline_cli:report['baselineExecutableSha256']=sha(args.baseline_cli.read_bytes())
    (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps([(r['name'],r['pages']) for r in results]))


if __name__=='__main__':main()
