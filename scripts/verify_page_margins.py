"""F3 static page text: direct entry parity and independent page/text/geometry checks."""
import argparse
import json
import subprocess
from pathlib import Path
from pypdf import PdfReader, PdfWriter
from PIL import Image
from fontTools.ttLib import TTFont
from pypdf.generic import ContentStream, FloatObject
from verify_font_styles import run, tool, sha, ROLES
from verify_tables import words
from verify_heading_keep import cases as heading_cases
from verify_table_pagination import cases as table_cases, html as table_html


def explicit(n):
    return ''.join(f"<p style='break-before:{'auto' if i==1 else 'page'};margin-top:0;margin-bottom:0'>PAGE</p>" for i in range(1,n+1))


def cases():
    heading={c[0]:c[1] for c in heading_cases()}
    return [
        dict(name='empty',html='',pages=1,header='Header',footer='Footer'),
        dict(name='header-literal',html='<p>BODY</p>',pages=1,header='<b>&amp; {page}</b>',footer=''),
        dict(name='footer-only',html='<p>BODY</p>',pages=1,header='',footer='  Internal   copy  '),
        dict(name='explicit-three',html=explicit(3)+"<p style='break-after:page'></p>",pages=3,header='Header',footer='Footer',numbers=True),
        dict(name='heading-tail',html=heading['tail'],pages=2,header='Heading report',footer='Internal',numbers=True),
        dict(name='table-thirty',html=table_html(table_cases()[1]),pages=2,header='Record register',footer='Internal',numbers=True),
        dict(name='bilingual',html=heading['bilingual'].replace("<h2 style='","<h2 style='line-height:3;"),pages=2,header='业务简报 / Report',footer='内部 / Internal',numbers=True,fonts=True),
        dict(name='latin-full',html='<p><strong>Bold</strong> <em>Italic</em></p>',pages=1,header='Summary',footer='Full font',numbers=True,fonts=True,full=True),
        dict(name='counter-ten',html=explicit(10),pages=10,header='Header',footer='Footer',numbers=True),
        dict(name='near-width',html='<p>BODY</p>',pages=1,header='H'*87,footer='F'*83,numbers=True)]


def normalize(text):
    return ' '.join(part for part in text.split(' ') if part)


def font_metrics(paths):
    result=[]
    for path in paths:
        with TTFont(path) as font:
            head=font['head'];os2=font['OS/2'];hhea=font['hhea']
            typo=os2.version>=4 and os2.fsSelection & 128
            result.append(dict(units=head.unitsPerEm,cmap=font.getBestCmap(),
                ascent=os2.sTypoAscender if typo else hhea.ascent,
                descent=os2.sTypoDescender if typo else hhea.descent,
                top=head.yMax,bottom=head.yMin))
    return result


def expected_baseline(text,fonts,height,top):
    # Derive the contract from source fonts, not the generated PDF coordinates.
    if fonts:
        first=fonts[0];scale=10/first['units'];a=first['ascent']*scale;d=-first['descent']*scale
        leading=(12-a-d)/2;a+=leading;d+=leading
        for character in text:
            font=next(f for f in fonts if ord(character) in f['cmap'])
            a=max(a,font['top']*10/font['units']);d=max(d,-font['bottom']*10/font['units'])
    else:a,d=9,3
    return height-18-(a-d)/2 if top else 18-(a-d)/2


def commands(page,reader):
    painting=[];blocks=[];block=None
    for values,op in ContentStream(page.get_contents(),reader).operations:
        if op==b'BT':
            assert block is None,'nested text';block=[]
        elif op==b'ET':
            assert block is not None,'unmatched text end';blocks.append(block);block=None
        elif block is not None:block.append((values,op))
        else:painting.append((op,tuple(str(v) for v in values)))
    assert block is None,'unterminated text'
    return painting,blocks


def inspect(pdf,plain,case,fonts,raster=True):
    run(tool('qpdf'),'--check',pdf)
    reader=PdfReader(pdf);old=PdfReader(plain)
    assert len(reader.pages)==len(old.pages)==case['pages'],'page count'
    actual,bbox=words(pdf);original,_=words(plain);bounds=[]
    for index,(page,items,body) in enumerate(zip(reader.pages,actual,original)):
        width=float(page.mediabox.width);height=float(page.mediabox.height)
        header=[w for w in items if w['yMax']<36]
        footer=[w for w in items if w['yMin']>height-36]
        middle=[w for w in items if w not in header and w not in footer]
        assert middle==body,'body text/geometry changed'
        expected_header=normalize(case['header']);expected_footer=normalize(case['footer'])
        if case.get('numbers'):expected_footer=(expected_footer+' | ' if expected_footer else '')+str(index+1)
        paint,blocks=commands(page,reader);old_paint,old_blocks=commands(old.pages[index],old)
        assert paint==old_paint,'body painting changed'
        added=blocks[len(old_blocks):];seen=set()
        for block in added:
            sizes=[float(v[1]) for v,op in block if op==b'Tf']
            colors=[list(map(float,v)) for v,op in block if op==b'rg']
            transforms=[list(map(float,v)) for v,op in block if op==b'Tm']
            assert sizes==[10] and colors==[[0,0,0]],'page text size/color'
            assert len(transforms)==1 and transforms[0][:4]==[1,0,0,1],'page text transform'
            y=transforms[0][5];top=y>height-36
            assert top or y<36,'page text outside margin'
            expected=expected_header if top else expected_footer
            assert expected,'unexpected page text'
            assert abs(y-expected_baseline(expected,fonts,height,top))<.002,'page text vertical center'
            seen.add('header' if top else 'footer')
        assert seen=={kind for kind,text in [('header',expected_header),('footer',expected_footer)] if text},'missing page text runs'
        for kind,group,expected in [('header',header,expected_header),('footer',footer,expected_footer)]:
            assert ' '.join(w['text'] for w in group)==expected,kind+' text/order'
            if not expected:continue
            xmin=min(w['xMin'] for w in group);xmax=max(w['xMax'] for w in group)
            ymin=min(w['yMin'] for w in group);ymax=max(w['yMax'] for w in group)
            assert abs((xmin+xmax)/2-width/2)<.02,kind+' center'
            assert xmin>=1.98 and xmax<=width-1.98,kind+' horizontal bounds'
            assert ymax-ymin<24,kind+' single line'
            if kind=='header':
                assert 1.98<=ymin<ymax<=36-1.98,'header margin'
                assert all(ymax+1.98<=w['yMin'] for w in body),'header/body overlap'
            else:
                assert height-36+1.98<=ymin<ymax<=height-1.98,'footer margin'
                assert all(w['yMax']+1.98<=ymin for w in body),'footer/body overlap'
            bounds.append(dict(page=index+1,kind=kind,text=expected,xMin=xmin,xMax=xmax,yMin=ymin,yMax=ymax))
    if raster:
        run(tool('pdftoppm'),'-png','-r','72',pdf,pdf.with_suffix(''))
        run(tool('pdftoppm'),'-png','-r','72',plain,plain.with_suffix(''))
        numbered=sorted(pdf.parent.glob(pdf.stem+'-*.png'));plain_images=sorted(plain.parent.glob(plain.stem+'-*.png'))
        assert len(numbered)==len(plain_images)==case['pages'],'raster page count'
        for rendered,original_image in zip(numbered,plain_images):
            with Image.open(rendered) as a,Image.open(original_image) as b:
                assert a.size==b.size,'raster dimensions'
                box=(0,36,a.width,a.height-36)
                assert a.crop(box).tobytes()==b.crop(box).tobytes(),'body raster changed'
        pdf.with_suffix('.bbox.html').write_text(bbox)
        pdf.with_suffix('.txt').write_bytes(run(tool('pdftotext'),'-raw',pdf,'-'))
    return dict(pages=case['pages'],sha256=sha(pdf.read_bytes()),bytes=pdf.stat().st_size,bounds=bounds,
                qpdf='pass',textOrderAndGeometry='pass',bodyUnchanged=True,bodyPaintingUnchanged=True,bodyRasterUnchanged=raster)


def reject_mutations(pdf,plain,case,fonts,out):
    rejected=[]
    for mode in ['wrong-header','wrong-footer','displaced-header','displaced-footer','small-shift-header','small-shift-footer','size-header']:
        reader=PdfReader(pdf);writer=PdfWriter()
        for page in reader.pages:writer.add_page(page)
        page=writer.pages[1];stream=ContentStream(page.get_contents(),writer)
        target='Header' if mode.endswith('header') else 'Footer | 2'
        at=next(i for i,(values,op) in enumerate(stream.operations) if op==b'Tj' and str(values[0])==target)
        if mode.startswith('wrong'):stream.operations[at][0][0]=type(stream.operations[at][0][0])('WRONG')
        elif mode.startswith('size'):
            at=next(i for i in range(at-1,-1,-1) if stream.operations[i][1]==b'Tf')
            stream.operations[at][0][1]=FloatObject(11)
        else:
            transform=next(i for i in range(at-1,-1,-1) if stream.operations[i][1]==b'Tm')
            stream.operations[transform][0][5]=FloatObject(float(stream.operations[transform][0][5])+3 if mode.startswith('small') else 400)
        page.replace_contents(stream);path=out/f'{mode}.pdf'
        with path.open('wb') as f:writer.write(f)
        try:inspect(path,plain,case,fonts,False)
        except AssertionError as e:rejected.append(dict(mode=mode,reason=str(e)))
        else:raise AssertionError('Accepted mutation '+mode)
        path.unlink()
    return rejected


def reject_paint_mutation(pdf,plain,case,fonts,out):
    reader=PdfReader(pdf);writer=PdfWriter()
    for page in reader.pages:writer.add_page(page)
    page=writer.pages[0];stream=ContentStream(page.get_contents(),writer)
    count=sum(op==b're' for _,op in stream.operations)
    assert count>0,'missing mutation target'
    stream.operations=[(v,op) for v,op in stream.operations if op!=b're'];page.replace_contents(stream)
    path=out/'removed-paint.pdf'
    with path.open('wb') as f:writer.write(f)
    try:inspect(path,plain,case,fonts,False)
    except AssertionError as e:result=dict(mode='removed-paint',removedRectangles=count,reason=str(e))
    else:raise AssertionError('Accepted missing table painting')
    path.unlink();return result


def main():
    p=argparse.ArgumentParser()
    for name in ['manifest','output','cli-dll','api-dll','native-cli']:p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--dotnet',required=True);p.add_argument('--baseline-cli',type=Path)
    args=p.parse_args();root=Path(__file__).resolve().parents[1];out=args.output.resolve();out.mkdir(parents=True,exist_ok=False)
    manifest=json.loads(args.manifest.read_text())
    assert all(sha(Path(f['path']).read_bytes())==f['sha256'] for f in manifest['fonts'])
    font_flags=[v for role,paths in manifest['roles'].items() for path in paths for v in (ROLES[role],path)]
    normal_fonts=font_metrics(manifest['roles']['normal'])
    results=[]
    for case in cases():
        name=case['name'];source=out/f'{name}.html';source.write_text(case['html']);fonts=normal_fonts if case.get('fonts') else []
        mode='full' if case.get('full') else 'subset';selection='fonts' if case.get('fonts') else 'courier'
        flags=(font_flags if case.get('fonts') else [])+([] if mode=='full' else ['--subset-fonts'])
        numbers=['--page-numbers'] if case.get('numbers') else []
        api=out/f'{name}-api.pdf';plain=out/f'{name}-body.pdf';compat=out/f'{name}-compat.pdf'
        api_command=[args.dotnet,args.api_dll,source]
        api_tail=[args.manifest,selection,mode,'on' if numbers else 'off']
        runtime=json.loads(run(*api_command,api,*api_tail,case['header'],case['footer']))
        run(*api_command,plain,args.manifest,selection,mode,'off','','')
        run(*api_command,compat,*api_tail,'','')
        for entry,command in [('cli',[args.dotnet,args.cli_dll]),('native',[args.native_cli])]:
            path=out/f'{name}-{entry}.pdf';run(*command,source,path,*flags,*numbers,'--header-text',case['header'],'--footer-text',case['footer'])
            assert path.read_bytes()==api.read_bytes(),'API/CLI/native bytes'
            off=out/f'{name}-compat-{entry}.pdf';run(*command,source,off,*flags,*numbers)
            assert off.read_bytes()==compat.read_bytes(),'disabled API/CLI/native bytes'
        result=dict(name=name,inputSha256=sha(source.read_bytes()),api=runtime,apiCliNativeByteEquality=True,
                    disabledApiCliNativeByteEquality=True,**inspect(api,plain,case,fonts))
        if args.baseline_cli:
            baseline=out/f'{name}-baseline.pdf';run(args.baseline_cli,source,baseline,*flags,*numbers)
            assert baseline.read_bytes()==compat.read_bytes(),'disabled differs from F2 baseline'
            result['baselineByteEquality']=True
        if name=='explicit-three':result['mutationsRejected']=reject_mutations(api,plain,case,fonts,out)
        if name=='table-thirty':result['mutationsRejected']=[reject_paint_mutation(api,plain,case,fonts,out)]
        results.append(result)
    negative=[]
    for name,header,footer,numbers,html,code,fonts in [
        ('wide-header','A'*88,'',False,'','FPDF1302',False),('wide-footer','','A'*88,False,'','FPDF1302',False),
        ('wrapped','A '*90,'',False,'','FPDF1302',False),('multiline','A\nB','',False,'','FPDF1305',False),
        ('missing-font','中','',False,'','FPDF1301',False),('raw-limit',' '*1025,'',False,'','FPDF1001',False),
        ('page-ten-overflow','','A'*83,True,explicit(10),'FPDF1302',False),
        ('font-body-overhang','业务简报 / Report','',False,dict((c[0],c[1]) for c in heading_cases())['bilingual'],'FPDF1302',True)]:
        source=out/f'negative-{name}.html';source.write_text(html)
        for entry in ['api','cli','native']:
            path=out/f'negative-{name}-{entry}.pdf';path.write_bytes(b'sentinel')
            if entry=='api':command=[args.dotnet,args.api_dll,source,path,args.manifest,'fonts' if fonts else 'courier','subset','on' if numbers else 'off',header,footer]
            else:command=([args.dotnet,args.cli_dll] if entry=='cli' else [args.native_cli])+[source,path,'--overwrite','--subset-fonts','--header-text',header,'--footer-text',footer]+(['--page-numbers'] if numbers else [])+(font_flags if fonts else [])
            process=subprocess.run([str(x) for x in command],capture_output=True)
            assert process.returncode==3 and code in process.stderr.decode(),(name,entry,process.returncode,process.stderr)
            assert path.read_bytes()==b'sentinel','failed conversion changed destination';path.unlink()
        negative.append(dict(name=name,code=code,apiCliNativeAtomicFailure=True))
    assert not list(out.glob('.factspdf-*.tmp')),'temporary file leak'
    report=dict(sourceHead=run('git','-C',root,'rev-parse','HEAD').decode().strip(),dirty=bool(run('git','-C',root,'status','--porcelain').strip()),
                cases=results,negative=negative,fonts=manifest['fonts'],visualReview='pending',chromePrint='not run locally; native engine evidence')
    if args.baseline_cli:report['baselineExecutableSha256']=sha(args.baseline_cli.read_bytes())
    (out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(cases=[(r['name'],r['pages']) for r in results],negative=len(negative))))


if __name__=='__main__':main()
