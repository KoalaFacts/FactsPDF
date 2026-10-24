"""F4 independent authored text, source-font, geometry and painting acceptance.
No generated reference images or corpus-generator expectations are consumed.
"""
import argparse
import io
import json
from pathlib import Path
import re
import subprocess

from fontTools.ttLib import TTFont
from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject
from verify_font_styles import run, tool, sha, ROLES
from verify_page_margins import expected_baseline, font_metrics, inspect as inspect_margins
from verify_operations_report import compact

H = 841.89
HEADER = 'FactsPDF | Quality review'
FOOTER = 'Internal copy'
INK = (24/255, 51/255, 72/255)
BORDER = (36/255, 68/255, 90/255)


def reference_status(): return 'pending-user-review'
def row_height(number): return 54 if number % 10 == 0 else 32


def row_groups(count):
    # Authored 362pt introduction + 32pt repeating header. Whole-row greedy fill.
    groups = [[]]; top = 398 + 32
    for number in range(1, count + 1):
        height = row_height(number)
        if top + height > H - 36:
            groups.append([]); top = 36 + 32
        groups[-1].append(number); top += height
    if top + 68 > H - 36: groups.append([])  # closing heading + two lines
    return groups


class Sources:
    def __init__(self, manifest):
        self.manifest = manifest
        self.fonts = {f['postscript']: TTFont(f['path']) for f in manifest['fonts']}
        self.by_path = {f['path']: f['postscript'] for f in manifest['fonts']}
        self.metrics = font_metrics(manifest['roles']['normal'])
        self.outlines = {}

    def font(self, role, char):
        return next(self.by_path[p] for p in self.manifest['roles'][role]
                    if ord(char) in self.fonts[self.by_path[p]].getBestCmap())

    def width(self, name, char, size):
        font = self.fonts[name]
        return font['hmtx'][font.getBestCmap()[ord(char)]][0] * size / font['head'].unitsPerEm

    def ascent(self, role, size):
        font = self.fonts[self.by_path[self.manifest['roles'][role][0]]]
        os2 = font['OS/2']; metric = os2 if os2.version >= 4 and os2.fsSelection & 128 else font['hhea']
        a, d = (metric.sTypoAscender, metric.sTypoDescender) if metric is os2 else (metric.ascent, metric.descent)
        return size + (a + d) * size / (2 * font['head'].unitsPerEm)  # line-height:2

    def check_embedded(self, resource):
        name = re.sub(r'^[A-Z]{6}\+', '', str(resource['/BaseFont']).lstrip('/'))
        assert name in self.fonts, 'unknown source font'
        original = self.fonts[name]; cid = resource['/DescendantFonts'][0].get_object()
        embedded = cid['/FontDescriptor']['/FontFile2'].get_data()
        mapping = resource['/ToUnicode'].get_data(); cmap = {}
        for block in re.findall(rb'beginbfchar\s+(.*?)\s+endbfchar', mapping, re.S):
            for code, utf16 in re.findall(rb'<([0-9A-F]+)>\s+<([0-9A-F]+)>', block):
                cmap[int(code, 16)] = bytes.fromhex(utf16.decode()).decode('utf-16-be')
        with TTFont(io.BytesIO(embedded)) as font:
            assert font['post'].italicAngle == original['post'].italicAngle, 'font slant'
            assert font['OS/2'].usWeightClass == original['OS/2'].usWeightClass, 'font weight'
            assert font['OS/2'].fsType == original['OS/2'].fsType, 'font permissions'
            assert set(font.getBestCmap()) == {ord(c) for c in cmap.values()}, 'subset cmap'
            gids = cid['/CIDToGIDMap'].get_data(); widths = cid['/W']
            assert int(widths[0]) == 1 and len(widths) == 2, 'CID widths structure'
            for code, char in cmap.items():
                old = original.getBestCmap()[ord(char)]; new = font.getBestCmap()[ord(char)]
                assert int.from_bytes(gids[code*2:code*2+2], 'big') == font.getGlyphID(new), 'CID glyph'
                assert font['hmtx'][new] == original['hmtx'][old], 'glyph advance'
                assert abs(float(widths[1][code-1]) - self.width(name, char, 1000)) < .00001, 'PDF glyph width'
                key = (name, char)
                if key not in self.outlines:
                    self.outlines[key] = tuple(map(list, original['glyf'][old].getCoordinates(original['glyf'])))
                assert tuple(map(list, font['glyf'][new].getCoordinates(font['glyf']))) == self.outlines[key], 'glyph outline'
        return name, cmap


def authored(count, sources, furniture=True, keep=True):
    pages = [[] for _ in row_groups(count)]; rectangles = [[] for _ in pages]
    page_text = [[] for _ in pages]

    def line(page, top, x, text, size=11, role='normal', segments=None, baseline=None, color=INK):
        segments = segments or [(text, role)]
        y = baseline if baseline is not None else H-top-max(sources.ascent(r, size) for r in {role, *[r for _, r in segments]})
        page_text[page].append(''.join(t for t, _ in segments))
        for text, selected_role in segments:
            for char in text:
                font = sources.font(selected_role, char)
                pages[page].append((char, font, size, x, y, color)); x += sources.width(font, char, size)

    def box(page, x, top, width, height, fill=None):
        def rect(x, y, w, h, color): rectangles[page].append((x, H-y-h, w, h, color))
        if fill is not None: rect(x, top, width, height, fill)
        rect(x, top, width, 1, BORDER); rect(x, top+height-1, width, 1, BORDER)
        rect(x, top+1, 1, height-2, BORDER); rect(x+width-1, top+1, 1, height-2, BORDER)

    line(0, 40, 36, '质量复核 / Quality review', 16, 'bold')
    line(0, 72, 36, f'REPORT-{count:03} 合成记录 / Synthetic records')
    line(0, 103, 45, '', segments=[('状态 ', 'normal'), ('通过', 'bold'), (' / ', 'normal'),
        ('Reviewed', 'italic'), (' / ', 'normal'), ('Priority', 'boldItalic')])
    box(0, 36, 94, 523.28, 40, (238/255,244/255,250/255))
    for text, top, x in [('核对记录 / Check records',134,58),('保留证据 / Retain evidence',156,80),('签署结论 / Sign decision',178,58)]:
        if x == 58:
            marker = '1.' if top == 134 else '2.'
            width = sum(sources.width(sources.font('normal', c), c, 11) for c in marker)
            line(0, top, 52.5-width, marker)
        line(0, top, x, text)
    box(0, 36, 200, 250, 130)
    line(0, 330, 36, 'CHART-01 合成示意 / Synthetic chart')
    line(0, 352, 36, '记录明细 / Record details', 12, 'bold')
    line(0, 376, 36, '逐行核对 / Review every row')
    groups = row_groups(count); final_top = 0
    for page, rows in enumerate(groups):
        top = 398 if page == 0 else 36
        if rows:
            for index, cells, height in [(0, ['ID','状态 / Status','说明 / Description'],32)] + [
                (n, [f'R{n:03}', '通过 / Pass', f'检查 {n:03} / Check {n:03}'], row_height(n)) for n in rows]:
                x = 36
                for column, (width, text) in enumerate(zip((80,160,280),cells)):
                    box(page,x,top,width,height,(220/255,233/255,242/255) if index == 0 else (1,1,1))
                    line(page,top+5,x+5,text,role='bold' if index == 0 else 'normal')
                    if index and index % 10 == 0 and column == 2:
                        line(page,top+27,x+5,f'NOTE-{index:03} 已复核 / reviewed')
                    x += width
                top += height
        final_top = top
    final = len(pages)-1
    # Counterfactual auto drops only the closing heading onto the 10-row first page.
    line(0 if count == 10 and not keep else final, 772 if count == 10 and not keep else final_top,
         36, '复核结论 / Review decision',12,'bold')
    paragraph_top = 36 if count == 10 and not keep else final_top+24
    line(final,paragraph_top,36,'记录完整 / Records complete')
    line(final,paragraph_top+22,36,f'END-{count:03} 已核准 / Accepted')
    if furniture:
        for page in range(len(pages)):
            for text, top in [(HEADER,True),(f'{FOOTER} | {page+1}',False)]:
                width = sum(sources.width(sources.font('normal', c),c,10) for c in text)
                line(page,0,(595.28-width)/2,text,10,baseline=expected_baseline(text,sources.metrics,H,top),color=(0,0,0))
    disc_y = H-156-sources.ascent('normal',11)+3.3
    return pages, rectangles, page_text, (72.85, disc_y, 1.65)


def painted(page, reader):
    rectangles=[]; discs=[]; images=[]; color=(0,0,0); matrix=[1,0,0,1,0,0]; stack=[]; path=[]; seen_text=False
    for values, op in ContentStream(page.get_contents(),reader).operations:
        assert op in (b'q',b'Q',b'rg',b'cm',b're',b'm',b'c',b'f',b'n',b'Do',b'BT',b'ET',b'Tf',b'Tm',b'Tj'), 'unexpected painting operator'
        if op == b'q': stack.append((color,matrix.copy()))
        elif op == b'Q': color,matrix=stack.pop()
        elif op == b'rg': color=tuple(map(float,values))
        elif op in (b'BT',b'Tj'):
            assert matrix == [1,0,0,1,0,0], 'text graphics transform'
            seen_text=True
        elif op == b'cm':
            assert matrix == [1,0,0,1,0,0], 'unexpected painting transform'
            matrix=list(map(float,values))
        elif op in (b're', b'm', b'c'): path.append((op,list(map(float,values))))
        elif op in (b'f',b'f*'):
            assert not seen_text, 'painting after text'
            assert matrix == [1,0,0,1,0,0], 'transformed painted path'
            if len(path) == 1 and path[0][0] == b're': rectangles.append((*path[0][1],color))
            else: discs.append((path,color))
            path=[]
        elif op == b'n': path=[]
        elif op == b'Do':
            assert not seen_text, 'painting after text'
            images.append((matrix.copy(),page['/Resources']['/XObject'][values[0]].get_object()))
    assert not stack and not path, 'unfinished painting state'
    return rectangles,discs,images


def actual_chars(page, reader, cache, sources):
    maps={}
    for key, ref in page['/Resources']['/Font'].items():
        resource=ref.get_object(); identity=ref.idnum
        if identity not in cache: cache[identity]=sources.check_embedded(resource)
        maps[key]=cache[identity]
    result=[]; matrix=None; font=None; size=None; color=(0,0,0); stack=[]
    for values,op in ContentStream(page.get_contents(),reader).operations:
        assert op not in (b'Tr',b'Tz',b'TJ',b'Td',b'TD',b'T*'), 'unexpected text transform'
        if op == b'q': stack.append((color,font,size))
        elif op == b'Q': color,font,size=stack.pop()
        elif op == b'rg': color=tuple(map(float,values))
        elif op == b'Tf': font=maps[values[0]];size=float(values[1])
        elif op == b'Tm':
            matrix=list(map(float,values));assert matrix[:4]==[1,0,0,1], 'text matrix'
        elif op == b'Tj':
            raw=values[0].original_bytes if hasattr(values[0],'original_bytes') else bytes(values[0])
            assert len(raw)%2 == 0, 'CID encoding'
            name,cmap=font;x,y=matrix[4:]
            for i in range(0,len(raw),2):
                char=cmap[int.from_bytes(raw[i:i+2],'big')]
                result.append((char,name,size,x,y,color));x+=sources.width(name,char,size)
    assert not stack, 'unfinished text graphics state'
    return result


def rounded(rect): return tuple(round(v,3) for v in rect[:4])+tuple(round(v,3) for v in rect[4])


def inspect(pdf,count,sources,jpeg,furniture=True,keep=True,raster=True):
    run(tool('qpdf'),'--check',pdf)
    reader=PdfReader(pdf);expected,boxes,texts,disc=authored(count,sources,furniture,keep)
    assert len(reader.pages)==len(expected), 'authored page count'
    cache={};all_images=[]
    for index,page in enumerate(reader.pages):
        assert abs(float(page.mediabox.width)-595.28)<.001 and abs(float(page.mediabox.height)-H)<.001, 'page size'
        chars=actual_chars(page,reader,cache,sources)
        assert ''.join(c[0] for c in chars)==''.join(c[0] for c in expected[index]), 'authored text/order'
        assert [(c[1],c[2]) for c in chars]==[(c[1],c[2]) for c in expected[index]], 'authored font role/size'
        assert all(abs(a[3]-b[3])<.002 and abs(a[4]-b[4])<.002 for a,b in zip(chars,expected[index])), 'authored text baseline/position'
        assert all(max(abs(x-y) for x,y in zip(a[5],b[5]))<.00051 for a,b in zip(chars,expected[index])), 'authored text color'
        rectangles,discs,images=painted(page,reader)
        assert list(map(rounded,rectangles))==list(map(rounded,boxes[index])), 'authored filled rectangles/colors'
        assert len(discs)==(1 if index==0 else 0), 'authored disc count'
        if discs:
            (path,color),=discs; x,y,r=disc;k=r*.5522847498307936
            desired=[(b'm',[x+r,y]),(b'c',[x+r,y+k,x+k,y+r,x,y+r]),(b'c',[x-k,y+r,x-r,y+k,x-r,y]),
                     (b'c',[x-r,y-k,x-k,y-r,x,y-r]),(b'c',[x+k,y-r,x+r,y-k,x+r,y])]
            assert [p[0] for p in path]==[p[0] for p in desired] and all(abs(a-b)<.002 for actual,wanted in zip(path,desired) for a,b in zip(actual[1],wanted[1])), 'authored disc geometry'
            assert max(abs(a-b) for a,b in zip(color,INK))<.00051, 'disc color'
        assert len(images)==(1 if index==0 else 0), 'authored image count'
        for matrix,image in images:
            assert matrix==[240,0,0,120,41,516.89], 'authored image geometry'
            assert image['/Subtype']=='/Image' and image['/Filter']=='/DCTDecode' and image['/Width']==320 and image['/Height']==160, 'image format'
            assert image._data==jpeg.read_bytes(), 'original JPEG bytes'
            all_images.append(matrix)
        # A second parser checks actual extraction, including English word separation.
        extracted=run(tool('pdftotext'),'-f',index+1,'-l',index+1,'-raw',pdf,'-').decode()
        assert compact(extracted)==compact('\n'.join(texts[index])), 'independent extracted text'
    result=dict(pages=len(expected),pdfSha256=sha(pdf.read_bytes()),bytes=pdf.stat().st_size,
        authoredTextAndPositions=True,sourceGlyphsAndRoles=True,paintedGeometryAndColors=True,
        jpegBytesAndPlacement=True,qpdf='pass',referenceApproval=reference_status())
    if raster:
        run(tool('pdftoppm'),'-png','-r','96',pdf,pdf.with_suffix(''))
        pngs=sorted(pdf.parent.glob(pdf.stem+'-*.png'));assert len(pngs)==len(expected),'raster page count'
        with Image.open(pngs[0]).convert('RGB') as image,Image.open(jpeg).convert('RGB') as original:
            for sx,sy in [(15,10),(304,10),(15,148),(304,148),(160,20)]:
                pixel=image.getpixel((round((41+(sx+.5)*240/320)*96/72),round((205+(sy+.5)*120/160)*96/72)))
                assert max(abs(a-b) for a,b in zip(pixel,original.getpixel((sx,sy))))<=12,'rendered JPEG color/orientation'
            for x,y,color in [(38,96,(238,244,250)),(38,400,(220,233,242)),(36.4,410,(36,68,90))]:
                pixel=image.getpixel((int(x*96/72),int(y*96/72)))
                assert max(abs(a-b) for a,b in zip(pixel,color))<=4,'rendered panel/table paint'
        result['pageImages']=[dict(page=i+1,file=p.name,sha256=sha(p.read_bytes())) for i,p in enumerate(pngs)]
    return result


def mutation_checks(pdf,count,sources,jpeg,out):
    """Real PDFs, a passing unmodified rewrite, and diagnostic-specific rejection."""
    reasons={
        'missing-record':'authored text/order','duplicate-record':'authored text/order','reordered-record':'authored text/order',
        'missing-repeated-header':'authored text/order','collapsed-note':'authored text baseline/position',
        'wrong-font-role':'authored font role/size','missing-marker':'authored text/order','missing-disc':'authored disc count',
        'missing-image':'authored image count','image-geometry':'authored image geometry','image-data':'original JPEG bytes',
        'unpainted-cell':'authored filled rectangles/colors','white-header':'authored filled rectangles/colors','reordered-cell-paint':'authored filled rectangles/colors',
        'shift-header':'authored text baseline/position','shift-footer':'authored text baseline/position',
        'wrong-counter':'authored text/order','detached-heading':'authored text baseline/position',
        'text-ctm':'text graphics transform','occluded-records':'painting after text','restored-text-color':'authored text color'}
    results=[]
    for mode in ['rewrite-control',*reasons]:
        reader=PdfReader(pdf);writer=PdfWriter();writer.append_pages_from_reader(reader)
        pageno=1 if mode in ('missing-repeated-header','wrong-counter','detached-heading','text-ctm','occluded-records','restored-text-color') else 0
        page=writer.pages[pageno];stream=ContentStream(page.get_contents(),writer);ops=stream.operations
        mappings={key:sources.check_embedded(ref.get_object()) for key,ref in page['/Resources']['/Font'].items()}
        blocks=[];start=None;font=None
        for i,(v,op) in enumerate(ops):
            if op==b'BT': start=i;text='';font=None
            elif op==b'Tf':font=v[0]
            elif op==b'Tj':
                raw=v[0].original_bytes if hasattr(v[0],'original_bytes') else bytes(v[0])
                text+=''.join(mappings[font][1][int.from_bytes(raw[j:j+2],'big')] for j in range(0,len(raw),2))
            elif op==b'ET':blocks.append((start,i+1,text))
        def block(text):return next(b for b in blocks if b[2]==text)
        def at(block,operator):return next(i for i in range(block[0],block[1]) if ops[i][1]==operator)
        if mode in ('missing-record','missing-repeated-header','missing-marker'):
            b=block({'missing-record':'R001','missing-repeated-header':'ID','missing-marker':'1.'}[mode]);del ops[b[0]:b[1]]
        elif mode=='duplicate-record':
            b=block('R001');ops[b[1]:b[1]]=ops[b[0]:b[1]]
        elif mode=='reordered-record':
            a,b=block('R001'),block('R002');ops[a[0]:b[1]]=ops[b[0]:b[1]]+ops[a[1]:b[0]]+ops[a[0]:a[1]]
        elif mode in ('collapsed-note','shift-header','shift-footer','detached-heading'):
            target={'collapsed-note':'NOTE-010 ','shift-header':HEADER,'shift-footer':FOOTER+' | 1',
                    'detached-heading':'Review decision'}[mode]
            b=next(b for b in blocks if target in b[2]);i=at(b,b'Tm')
            ops[i][0][5]=FloatObject(float(ops[i][0][5])+(22 if mode=='collapsed-note' else 3))
        elif mode in ('wrong-font-role','wrong-counter'):
            b=block('ID' if mode=='wrong-font-role' else FOOTER+' | 2');tf=at(b,b'Tf');tj=at(b,b'Tj')
            resource=next(key for key,(name,_) in mappings.items() if name=='DejaVuSans') if mode=='wrong-font-role' else ops[tf][0][0]
            inverse={char:code for code,char in mappings[resource][1].items()};text=b[2] if mode=='wrong-font-role' else FOOTER+' | 1'
            from pypdf.generic import ByteStringObject
            ops[tf][0][0]=resource;ops[tj][0][0]=ByteStringObject(b''.join(inverse[c].to_bytes(2,'big') for c in text))
        elif mode=='missing-disc':
            i=next(i for i,(_,op) in enumerate(ops) if op==b'c');j=next(j for j in range(i,len(ops)) if ops[j][1]==b'f');ops[j]=([],b'n')
        elif mode=='missing-image':ops[:]=[(v,op) for v,op in ops if op!=b'Do']
        elif mode=='image-geometry':
            i=next(i for i,(_,op) in enumerate(ops) if op==b'cm');ops[i][0][0]=FloatObject(241)
        elif mode=='image-data':next(iter(page['/Resources']['/XObject'].values())).get_object()._data+=b'X'
        elif mode in ('unpainted-cell','white-header'):
            i=next(i for i,(v,op) in enumerate(ops) if op==b're' and list(map(float,v))==[36,411.89,80,32])
            if mode=='unpainted-cell':ops[i+1]=([],b'n')
            else:ops[i-1]=([FloatObject(1)]*3,b'rg')
        elif mode=='restored-text-color':
            changed=[];active=False
            for values,op in ops:
                if op==b'BT':active=True
                if active and op==b'rg':changed.extend([([FloatObject(1)]*3,b'rg'),([],b'q'),(values,op),([],b'Q')])
                else:changed.append((values,op))
                if op==b'ET':active=False
            stream.operations=changed
        elif mode=='reordered-cell-paint':
            i=next(i for i,(v,op) in enumerate(ops) if op==b're' and list(map(float,v))==[36,411.89,80,32])-2
            assert ops[i][1]==b'q' and ops[i+4][1]==b'Q', 'paint mutation group'
            ops[i:i+25]=ops[i+5:i+25]+ops[i:i+5]
        elif mode=='text-ctm':
            changed=[]
            for values,op in ops:
                if op==b'BT':changed.extend([([],b'q'),([FloatObject(v) for v in (1,0,0,1,30,0)],b'cm')])
                changed.append((values,op))
                if op==b'ET':changed.append(([],b'Q'))
            stream.operations=changed
        elif mode=='occluded-records':
            text_ops=[];painting=[];active=False
            for values,op in ops:
                if op==b'BT':active=True
                (text_ops if active else painting).append((values,op))
                if op==b'ET':active=False
            # Retain correct text colors while moving opaque cell fills last.
            changed=[]
            for values,op in text_ops:
                if op==b'BT':changed.append(([FloatObject(v) for v in INK],b'rg'))
                if op==b'Tf' and float(values[1])==10:changed.append(([FloatObject(0)]*3,b'rg'))
                changed.append((values,op))
            stream.operations=changed+painting
        page.replace_contents(stream);path=out/f'mutant-{mode}.pdf'
        with path.open('wb') as f:writer.write(f)
        try:inspect(path,count,sources,jpeg,raster=False)
        except AssertionError as error:
            assert mode!='rewrite-control' and str(error)==reasons[mode],(mode,str(error))
            results.append(dict(mutation=mode,rejectedBy=str(error)))
        else:assert mode=='rewrite-control','accepted mutant '+mode
        path.unlink()
    return dict(unmodifiedRewrite='pass',rejected=results)


def baseline_approvals(root):
    registry=json.loads((root/'docs/acceptance/reference-reviews.json').read_text())
    # Record the existing approval registry unchanged; its original source binding
    # does not transfer human approval to this run or these new combined pages.
    approved = []
    for review in registry['reviews']:
        assert review['approval']=='approved' and review['reviewer_kind']=='human', 'original human approval'
        folder=root/review['artifact_location']
        assert sha((folder/'native.pdf').read_bytes())==review['pdf_sha256'], 'approved PDF hash'
        assert sha((folder/'text.txt').read_bytes())==review['text_sha256'], 'approved text hash'
        assert len(review['page_image_sha256'])==review['pages'], 'approved page count'
        for i,digest in enumerate(review['page_image_sha256'],1):
            assert sha((folder/f'page-{i}.png').read_bytes())==digest, 'approved page image hash'
        approved.append(dict(case=review['case_id'],pages=review['pages'],sourceSha=review['source_sha'],hashesVerified=True))
    assert len(approved)==6 and sum(r['pages'] for r in approved)==10, 'original approval denominator'
    return dict(originalReferences=approved,registrySha256=sha((root/'docs/acceptance/reference-reviews.json').read_bytes()),
                humanApprovedCases=sum(r['approval']=='approved' and r['reviewer_kind']=='human' for r in registry['reviews']),
                originalTargetDenominator=9,newCombinationApproval=reference_status())


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('manifest','output','cli-dll','api-dll','native-cli'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--dotnet',required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parents[1];out=args.output.resolve();out.mkdir(parents=True,exist_ok=False)
    manifest=json.loads(args.manifest.read_text());sources=Sources(manifest)
    assert all(sha(Path(f['path']).read_bytes())==f['sha256'] for f in manifest['fonts']),'source font hashes'
    jpeg=root/'tests/fixtures/jpeg/rgb-420.jpg'
    fonts=[v for role,paths in manifest['roles'].items() for path in paths for v in (ROLES[role],path)]
    common=['--subset-fonts','--image',f'quality-chart={jpeg}',*fonts]
    furniture=['--page-numbers','--header-text',HEADER,'--footer-text',FOOTER]
    entries={'managed':[args.dotnet,args.cli_dll],'native':[args.native_cli]}
    evidence=dict(sourceHead=run('git','rev-parse','HEAD').decode().strip(),
        dirty=bool(run('git','status','--porcelain').strip()),fonts=[{k:v for k,v in f.items() if k!='path'} for f in manifest['fonts']],
        approvals=baseline_approvals(root),cases=[],negativeCases=[],visualReview='pending',browserPrint='not run')
    for count in (10,30,100):
        source=root/f'examples/combined-report-{count}.html'
        for variant in (['positive','body','auto'] if count==10 else ['positive','body']):
            name=f'report-{count}-{variant}';enabled=variant!='body'
            input_path=source
            if variant=='auto':
                input_path=out/(name+'.html');input_path.write_text(source.read_text().replace('break-after:avoid','break-after:auto'))
            pdf=out/(name+'-api.pdf');meta=json.loads(run(args.dotnet,args.api_dll,input_path,pdf,args.manifest,jpeg,'on' if enabled else 'off'))
            for entry,command in entries.items():
                path=out/(name+'-'+entry+'.pdf');run(*command,input_path,path,*common,*(furniture if enabled else []))
                assert path.read_bytes()==pdf.read_bytes(),'API/CLI/NativeAOT bytes'
            # Inspect/rasterize the actual NativeAOT output, not just the API file.
            native=out/(name+'-native.pdf')
            result=inspect(native,count,sources,jpeg,enabled,variant!='auto')
            evidence['cases'].append(dict(name=name,records=count,variant=variant,inputSha256=sha(input_path.read_bytes()),
                api=meta,apiCliNativeBytesEqual=True,**result))
        positive=out/f'report-{count}-positive-native.pdf';plain=out/f'report-{count}-body-native.pdf'
        inspect_margins(positive,plain,dict(pages=len(row_groups(count)),header=HEADER,footer=FOOTER,numbers=True),sources.metrics,False)
        for a,b in zip(sorted(out.glob(positive.stem+'-*.png')),sorted(out.glob(plain.stem+'-*.png'))):
            with Image.open(a) as x,Image.open(b) as y:
                crop=(0,48,x.width,round((H-36)*96/72))
                assert x.crop(crop).tobytes()==y.crop(crop).tobytes(),'disabled furniture body raster'
    evidence['mutations']=mutation_checks(out/'report-30-positive-native.pdf',30,sources,jpeg,out)
    negatives={'missing-image':'FPDF1604','missing-bold':'FPDF1507','wide-row':'FPDF1302','tall-row':'FPDF1302',
               'long-footer':'FPDF1302','oversize-footer':'FPDF1001','page-limit':'FPDF1303','output-limit':'FPDF1401','display-limit':'FPDF1401'}
    original=(root/'examples/combined-report-10.html').read_text()
    for mode,code in negatives.items():
        html=original
        if mode=='wide-row':html=html.replace('<td>R001</td>','<td>'+'W'*200+'</td>')
        elif mode=='tall-row':html=html.replace('<td>R001</td>','<td style="font-size:80pt;line-height:10">X</td>')
        source=out/(mode+'.html');source.write_text(html)
        api=json.loads(run(args.dotnet,args.api_dll,source,out/'must-not-exist.pdf',args.manifest,jpeg,'on',mode))
        assert not (out/'must-not-exist.pdf').exists(),'negative API wrote a file'
        evidence['negativeCases'].append(dict(entry='api',**api))
        if mode in ('page-limit','output-limit','display-limit'):continue  # API-only options
        flags=[v for role,paths in manifest['roles'].items() if not (mode=='missing-bold' and role=='bold') for path in paths for v in (ROLES[role],path)]
        flags=['--subset-fonts',*flags,*([] if mode=='missing-image' else ['--image',f'quality-chart={jpeg}']),
               '--page-numbers','--header-text',HEADER,'--footer-text','W'*(1025 if mode=='oversize-footer' else 100) if mode in ('oversize-footer','long-footer') else FOOTER]
        for entry,command in entries.items():
            for target in ('stdout','existing','new'):
                path=out/f'{mode}-{entry}-{target}.pdf'
                if target=='existing':path.write_bytes(b'sentinel')
                process=subprocess.run([str(v) for v in [*command,source,'-' if target=='stdout' else path,'--overwrite',*flags]],capture_output=True,timeout=120)
                assert process.returncode==3 and code.encode() in process.stderr and not process.stdout,(mode,process.stderr)
                assert path.read_bytes()==b'sentinel' if target=='existing' else not path.exists(),'partial output'
                assert not list(out.glob('.factspdf-*.tmp')),'temporary output leak'
                if target=='existing':path.unlink()
            evidence['negativeCases'].append(dict(mode=mode,entry=entry,code=code,stdoutEmpty=True,existingPreserved=True,newAbsent=True))
    for entry,command in entries.items():
        path=out/f'recovery-{entry}.pdf'
        run(*command,root/'examples/combined-report-10.html',path,*common,*furniture)
        assert path.read_bytes()==(out/'report-10-positive-native.pdf').read_bytes(),'recovery byte equality'
    evidence['positivePages']=sum(c['pages'] for c in evidence['cases'] if c['variant']=='positive')
    evidence['allGeneratedVariantPages']=sum(c['pages'] for c in evidence['cases'])
    (out/'report.json').write_text(json.dumps(evidence,indent=2,ensure_ascii=False)+'\n')
    print(json.dumps(dict(cases=len(evidence['cases']),pages=evidence['allGeneratedVariantPages'],report=str(out/'report.json'))))


if __name__=='__main__':main()
