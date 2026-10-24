"""Independent actual-PDF acceptance for continued body rows.

All expected geometry below is authored from the approved design equations,
before candidate output exists. This inspector never imports renderer internals.
Small 240x100pt cases use the public API host; CLI/native/browser use A4 only.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess

import PIL
from PIL import Image
import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, FloatObject, TextStringObject

if __package__:
    from .png_fixtures import png
else:
    from png_fixtures import png

CELL = [228, 243, 240]
TABLE = [243, 218, 172]
BORDER = [36, 68, 90]
IMAGE_BORDER = [204, 85, 0]
WHITE = [255, 255, 255]
QUADRANTS = [(240, 20, 40, 255), (20, 220, 60, 128),
             (30, 50, 230, 0), (40, 70, 200, 255)]
SMALL = {'PageWidth': 240, 'PageHeight': 100, 'Margin': 10}


def run(command):
    result = subprocess.run([str(x) for x in command], capture_output=True, timeout=120)
    assert result.returncode == 0, f'Command failed: {command}\n{result.stderr.decode(errors="replace")}'
    return result.stdout


def text(value, x, top, page=0, cell='main'):
    return {'value': value, 'x': x, 'top': top, 'page': page, 'cell': cell}


def mark(x, top, color, page=0, reason='cell/table occupied edge'):
    return {'x': x, 'top': top, 'color': color, 'page': page, 'reason': reason}


def picture(x, top, page=1, size=24):
    return {'x': x, 'top': top, 'width': size, 'height': size, 'page': page,
            'background': CELL, 'padding': 2, 'border': 1}


def rect(page, x, top, width, height, color):
    return {'page':page,'x':x,'top':top,'width':width,'height':height,'color':color}


def box(page, x, top, width, height, *, first=True, last=True):
    result=[rect(page,x,top,width,height,CELL)]
    if first: result.append(rect(page,x,top,width,1,BORDER))
    if last: result.append(rect(page,x,top+height-1,width,1,BORDER))
    middle=height-int(first)-int(last)
    if middle>0:
        result += [rect(page,x,top+int(first),1,middle,BORDER),
                   rect(page,x+width-1,top+int(first),1,middle,BORDER)]
    return result


def table(cells, tracks=(100, 100), *, header='HEAD', edges=False, bottom=0,
          top=0, second='', table_width=None):
    width = sum(tracks) if table_width is None else table_width
    style = ('<style>table{font-size:10pt;line-height:1.2;background-color:#f3daac;'
             f'width:{width}pt;padding-top:{top}pt;padding-bottom:{bottom}pt}}'
             'td,th{padding:0;background-color:#e4f3f0;'
             + ('border:1pt solid #24445a' if edges else 'border:none') + '}'
             'th{font-weight:normal}p{margin-top:0;margin-bottom:0}img{width:24pt;padding:2pt;'
             'border:1pt solid #cc5500}</style>')
    columns = '<colgroup>' + ''.join(f'<col style="width:{w}pt">' for w in tracks) + '</colgroup>'
    heading = (f'<thead><tr><th colspan="{len(tracks)}">{header}</th></tr></thead>' if header else '')
    return style + '<table>' + columns + heading + '<tbody><tr>' + cells + '</tr>' + second + '</tbody></table>'


def lines(prefix, count, digits=0):
    return '<br>'.join(f'{prefix}{n:0{digits}}' for n in range(1, count + 1))


def create(directory):
    """Materialize fixed input and literal expectations, independent of any binaries."""
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    rgba = bytes(c for y in range(32) for x in range(32)
                 for c in QUADRANTS[(2 if y >= 16 else 0) + (1 if x >= 16 else 0)])
    (directory / 'sample.png').write_bytes(png(32, 32, 6, 8,
        [rgba[y * 128:(y + 1) * 128] for y in range(32)]))
    cases = []

    def add(name, html, texts, pages, *, a4=False, witnesses=(), images=(), commands=None, rectangles=None):
        cases.append({'name': name, 'html': html, 'text': texts, 'pages': pages,
                      'options': {} if a4 else SMALL, 'width': 595.28 if a4 else 240,
                      'height': 841.89 if a4 else 100, 'witnesses': list(witnesses),
                      'images': list(images), 'commands': commands, 'rectangles':rectangles,
                      'entry': 'cli' if a4 else 'api'})

    # S1 and A1: exact source order is checked independently inside each logical cell.
    for name, count, a4, per_page, starts, ends in [
        ('S1', 12, False, [5, 5, 2], [34, 33, 33], [85, 84, 49]),
        ('A1', 130, True, [62, 62, 6], [60, 59, 59], [795, 794, 123])]:
        margin = 36 if a4 else 10
        html = table('<td>ID</td><td>' + lines('L', count, 3 if a4 else 2) + '</td><td>9</td>',
                     (40, 120, 40), edges=True)
        expected, witnesses, rectangles, number = [], [], [], 1
        for page, n in enumerate(per_page):
            expected += [text('HEAD', margin + 1, margin + 10, page, 'header')]
            if page == 0:
                expected += [text('ID', margin + 1, starts[page], page, 'id'),
                             text('9', margin + 161, starts[page], page, 'price')]
            expected += [text(f'L{k:0{3 if a4 else 2}}', margin + 41,
                              starts[page] + 12 * i, page, 'description')
                         for i, k in enumerate(range(number, number + n))]
            number += n
            table_end=805.89 if a4 else 90
            rectangles += [rect(page,margin,margin,200,(table_end if page<2 else ends[page])-margin,TABLE)]
            rectangles += box(page,margin,margin,200,14)
            for x,width in [(margin,40),(margin+40,120),(margin+160,40)]:
                rectangles += box(page,x,margin+14,width,ends[page]-margin-14,first=page==0,last=page==2)
            if page < 2:
                witnesses += [mark(margin + 50, ends[page] - 1, CELL, page),
                              mark(margin + 50, ends[page] + 1, TABLE, page),
                              mark(margin + 40.5, ends[page] + 1, TABLE, page,
                                   'cell side stops at occupied extent')]
            witnesses += [mark(margin + 40.5, margin + 3, CELL, page, 'header spans tracks'),
                          mark(margin + 160.5, margin + 3, CELL, page, 'header spans tracks')]
            if page == 1:
                witnesses += [mark(margin + 50, margin + 14.5, CELL, page,
                                   'no internal top cut'),
                              mark(margin + 50, ends[page] - .5, CELL, page, 'no internal bottom cut')]
        witnesses += [mark(margin + 50, ends[2] - .5, BORDER, 2, 'terminal bottom edge'),
                      mark(margin + 50, ends[2] + 1, WHITE, 2, 'terminal table end')]
        add(name, html, expected, 3, a4=a4, witnesses=witnesses, commands=186 if a4 else 68,rectangles=rectangles)

    # S2: independent cursors and line heights. A blank line consumes geometry without text.
    for blank in (False, True):
        right = lines('B', 9)
        if blank:
            right = right.replace('B2<br>B3', 'B2<br><br>B3')
        html = table('<td style="line-height:2">' + lines('A', 4) + '</td><td>' + right + '</td>')
        expected = [text('HEAD', 10, 19, p, 'header') for p in range(2)]
        expected += [text(f'A{i+1}', 10, 35 + 20 * i, 0, 'left') for i in range(3)] + [text('A4', 10, 35, 1, 'left')]
        right_pages = [[(1,31),(2,43),(3,67),(4,79)], [(5,31),(6,43),(7,55),(8,67),(9,79)]] if blank else [
            [(1,31),(2,43),(3,55),(4,67),(5,79)], [(6,31),(7,43),(8,55),(9,67)]]
        expected += [text(f'B{n}', 110, baseline, page, 'right')
                     for page, values in enumerate(right_pages) for n, baseline in values]
        add('S2-blank' if blank else 'S2', html, expected, 2,
            witnesses=[mark(105, 81, CELL), mark(105, 83, TABLE),
                       mark(105, 81 if blank else 69, CELL, 1),
                       mark(105, 83 if blank else 71, WHITE, 1)])

    for name, n, a4 in [('S3', 4, False), ('A2', 62, True)]:
        html = table('<td>' + lines('A', n, 3 if a4 else 0) + '<img src="sample">TAIL</td><td>'
                     + ('ID' if a4 else lines('B', 7)) + '</td>')
        margin, body_top = (36, 48) if a4 else (10, 22)
        expected = [text('HEAD', margin, margin + 9, p, 'header') for p in range(2)]
        expected += [text(f'A{i+1:0{3 if a4 else 0}}', margin, body_top + 9 + 12*i, 0, 'left') for i in range(n)]
        expected += [text('TAIL', margin, body_top + 39, 1, 'left')]
        if a4:
            expected += [text('ID', margin + 100, body_top + 9, 0, 'right')]
        else:
            expected += [text(f'B{i+1}', 110, 31 + 12*i, 0, 'right') for i in range(5)]
            expected += [text('B6', 110, 31, 1, 'right'), text('B7', 110, 43, 1, 'right')]
        add(name, html, expected, 2, a4=a4, images=[picture(margin + 3, body_top + 3)],
            witnesses=[mark(margin+95, body_top + (744 if a4 else 60) - 1, CELL),
                       mark(margin+95, body_top + (744 if a4 else 60) + 1, TABLE),
                       mark(margin+95, body_top + 41, CELL, 1), mark(margin+95, body_top+43, WHITE, 1)])

    # S4/A3: terminal table padding discriminates exact fit from last-unit rollback.
    for a4, n, bottom_exact, bottom_short in [(False, 6, 19, 20), (True, 76, 8.89, 9.89)]:
        for short in (False, True):
            name = ('A3' if a4 else 'S4') + ('-short' if short else '-exact')
            bottom = bottom_short if short else bottom_exact
            html = table('<td style="line-height:2">' + lines('L', n, 3 if a4 else 0) + '</td>',
                         (100,), header=None, edges=True, bottom=bottom)
            counts = ([38,37,1] if short else [38,38]) if a4 else ([3,2,1] if short else [3,3])
            margin = 36 if a4 else 10
            expected, witnesses, rectangles, number = [], [], [], 1
            for page, count in enumerate(counts):
                expected += [text(f'L{i:0{3 if a4 else 0}}', margin + 1,
                                  margin + (14 if page == 0 else 13) + 20*k, page)
                             for k, i in enumerate(range(number, number + count))]
                number += count
                end = margin + (1 if page == 0 else 0) + 20*count + (1 if page == len(counts)-1 else 0)
                table_end=(805.89 if a4 else 90) if page<len(counts)-1 else end+bottom
                rectangles += [rect(page,margin,margin,100,table_end-margin,TABLE)]
                rectangles += box(page,margin,margin,100,end-margin,first=page==0,last=page==len(counts)-1)
                if page < len(counts)-1:
                    witnesses += [mark(margin+50, end-.5, CELL, page, 'no internal bottom cut'),
                                  mark(margin+50, end+.5, TABLE, page)]
                else:
                    witnesses += [mark(margin+50, end-.5, BORDER, page, 'terminal bottom edge'),
                                  mark(margin+50, end+.5, TABLE, page),
                                  mark(margin+50, end+bottom+.5, WHITE, page, 'terminal table padding')]
            add(name, html, expected, len(counts), a4=a4, witnesses=witnesses,
                commands=None if a4 else (20 if short else 16),rectangles=rectangles)

    # S5's physically 80pt row is eligible because a 12pt header must also fit.
    for pressure in ('none', 'ancestor', 'table'):
        html = table('<td style="line-height:2">' + lines('L', 4) + '</td>', (200,), top=10 if pressure=='table' else 0)
        if pressure == 'ancestor':
            html = '<div style="padding-top:10pt">' + html + '</div>'
        pressured = pressure != 'none'
        counts = [2,2] if pressured else [3,1]
        expected = [text('HEAD', 10, 29 if pressured else 19, 0, 'header'), text('HEAD', 10, 19, 1, 'header')]
        number = 1
        for page, count in enumerate(counts):
            expected += [text(f'L{i}', 10, (45 if pressured and page==0 else 35) + 20*k, page)
                         for k, i in enumerate(range(number, number+count))]
            number += count
        add('S5-'+pressure, html, expected, 2)

    # S6: colspan outer width100, content88; 50% source pixels44pt square.
    html = table('<td colspan="2" style="padding:5%;border:1pt solid #24445a">'
                 + lines('S', 8) + '<img src="sample" style="width:50%">END</td><td>Z</td>', (40,60,100))
    expected = [text('HEAD',10,19,p,'header') for p in range(3)]
    expected += [text(f'S{i}',16,37+12*(i-1),0,'span') for i in range(1,6)]
    expected += [text(f'S{i}',16,31+12*(i-6),1,'span') for i in range(6,9)]
    expected += [text('END',16,81,2,'span'),text('Z',110,31,0,'right')]
    # Final span: image outer50, END12, bottom6 =>68 fits body68 exactly.
    span_image = picture(19,25,page=2,size=44)
    add('S6-span',html,expected,3,images=[span_image],witnesses=[
        mark(50.5,24,CELL,p,'no internal span boundary') for p in range(3)])

    # Eight-point paragraph gap stays on same fragment, trims at continuation.
    html = table('<td><p style="margin-bottom:8pt">P1</p><p style="margin-top:4pt">P2</p>'
                 '<p>' + lines('R',6) + '</p></td><td>Z</td>')
    expected = [text('HEAD',10,19,p,'header') for p in range(2)] + [text('P1',10,31),text('P2',10,51),text('Z',110,31,cell='right')]
    expected += [text('R1',10,63),text('R2',10,75),text('R3',10,87)] + [text(f'R{i}',10,31+12*(i-4),1) for i in range(4,7)]
    add('S6-margins',html,expected,2)
    html = table('<td>' + lines('Q',5) + '<p style="margin-top:8pt">NEXT</p></td><td>Z</td>')
    expected = [text('HEAD',10,19,p,'header') for p in range(2)] + [text(f'Q{i}',10,31+12*(i-1)) for i in range(1,6)]
    expected += [text('NEXT',10,31,1),text('Z',110,31,cell='right')]
    add('S6-trim',html,expected,2)

    empty=copy.deepcopy(next(c for c in cases if c['name']=='S2'))
    empty.update(name='S6-empty-paragraphs',html=empty['html'].replace(
        '<td style="line-height:2">','<td style="line-height:2"><p></p><p> </p>'))
    cases.append(empty)
    for variant in ('terminal-empty-p','trailing-zero-row'):
        original=next(c for c in cases if c['name']=='S4-exact')
        extra=copy.deepcopy(original); extra['name']='S4-'+variant
        if variant=='terminal-empty-p':
            extra['html']=extra['html'].replace('padding-bottom:19pt','padding-bottom:16pt').replace(
                'L6</td>','L6<p style="margin-top:0;margin-bottom:3pt"></p></td>')
            # Three points move from table padding into the final cell's terminal margin.
            extra['rectangles']=None; extra['commands']=None
            extra['witnesses']=[mark(60,73.5,BORDER,1,'terminal empty paragraph bottom'),
                                mark(60,74.5,TABLE,1),mark(60,90.5,WHITE,1)]
        else:
            extra['html']=extra['html'].replace('</tbody>','<tr><td style="border:none;background-color:transparent"></td></tr></tbody>')
        cases.append(extra)

    html=table('<td><p style="margin-top:56pt">FIT</p></td><td>'+lines('B',9)+'</td>')
    expected=[text('HEAD',10,19,p,'header') for p in range(2)]+[text('FIT',10,87,0,'left')]
    expected += [text(f'B{i}',110,31+12*(i-1),0,'right') for i in range(1,6)]
    expected += [text(f'B{i}',110,31+12*(i-6),1,'right') for i in range(6,10)]
    add('S5-initial-exact',html,expected,2,witnesses=[mark(105,89,CELL),mark(105,91,WHITE)])

    html = ('<p style="font-size:5pt;line-height:1;margin-top:0;margin-bottom:0">PRE</p>' + table(
        '<td><p style="font-size:10pt;line-height:1;margin-top:70pt;margin-bottom:0">A</p></td>'
        '<td style="line-height:5">B1<br>B2<br>B3</td>', header=None))
    add('S7',html,[text('PRE',10,14,0,'prelude'),text('A',10,88,1,'left'),
                  text('B1',110,43,0,'right'),text('B2',110,38,1,'right'),text('B3',110,38,2,'right')],3,
        witnesses=[mark(105,64,CELL),mark(105,66,TABLE),mark(105,89,CELL,1),mark(105,91,WHITE,1)])

    errors = []
    for name, style, content in [('initial-margin','', '<p style="margin-top:69pt">X</p>'),
                                  ('initial-padding','padding-top:69pt','X'),
                                  ('atomic-line','line-height:8.1','X'),
                                  ('atomic-image','', '<img src="sample" style="width:81pt;padding:0;border:none">')]:
        errors.append({'name':name,'html':table(f'<td style="{style}">{content}</td><td>'+lines('N',100)+'</td>'),
                       'options':SMALL,'code':'FPDF1302'})
    errors += [{'name':'short-ancestor','html':'<div style="padding-top:1pt">'+table('<td style="line-height:2">A<br>B<br>C<br>D</td>',(200,),header=None)+'</div>',
                'options':SMALL,'code':'FPDF1302'},
               {'name':'atomic-header','html':table('<td>X</td><td>'+lines('B',9)+'</td>',header='<span style="line-height:8.1">HEAD</span>'),
                'options':SMALL,'code':'FPDF1302'},
               {'name':'empty-initial-margin','html':table('<td><p style="margin-top:100pt"></p></td><td>'+lines('B',9)+'</td>'),
                'options':SMALL,'code':'FPDF1302'},
               {'name':'a4-atomic-line','html':table('<td style="font-size:80pt;line-height:10">X</td>',(200,),header=None),
                'options':{},'code':'FPDF1302'}]
    manifest = {'source':'literal approved spec, authored independently before candidate output',
                'base':'28588111d3ab453a21d9edfe4a8835efc65ed448','referenceApproval':'pending-user-review',
                'image':{'file':'sample.png','width':32,'height':32,'rgba':list(rgba)},'cases':cases,'errors':errors}
    (directory/'table-row-fragments.json').write_text(json.dumps(manifest,separators=(',',':'))+'\n')
    for case in cases + errors:
        (directory/(case['name']+'.html')).write_text(case['html'])
    return manifest


def inspect(path, case, source_image, *, qpdf=None, raster=True):
    path = Path(path)
    reader = PdfReader(path, strict=True)
    assert len(reader.pages)==case['pages'], 'page count differs from literal expectation'
    all_text, placements, image_ids, mask_ids, checked = [], [], set(), set(), 0
    rectangles=[]
    rgba = bytes(source_image['rgba'])
    rgb = bytes(v for at in range(0,len(rgba),4) for v in rgba[at:at+3])
    for page_number,page in enumerate(reader.pages):
        assert abs(float(page.mediabox.width)-case['width']) <= .002
        assert abs(float(page.mediabox.height)-case['height']) <= .002
        matrix = tm = color = pending_rectangle = None
        for operands,operator in ContentStream(page.get_contents(),reader).operations:
            if operator==b'cm': matrix=list(map(float,operands))
            elif operator==b'rg': color=list(map(float,operands))
            elif operator==b're': pending_rectangle=list(map(float,operands))
            elif operator==b'f':
                assert pending_rectangle is not None and color is not None,'unsupported rectangle stream'
                x,bottom,w,h=pending_rectangle
                rectangles.append((page_number,x,case['height']-bottom-h,w,h,color))
                pending_rectangle=None
            elif operator==b'Tm': tm=list(map(float,operands))
            elif operator==b'Tj':
                assert tm is not None
                all_text.append((page_number,str(operands[0]),tm[4],case['height']-tm[5]))
            elif operator==b'Do':
                assert matrix is not None
                w,a,b,h,x,bottom=matrix
                assert a==b==0,'unexpected image rotation'
                placements.append((page_number,x,case['height']-bottom-h,w,h))
                reference=page['/Resources']['/XObject'].raw_get(operands[0]); obj=reference.get_object()
                image_ids.add(reference.idnum)
                assert obj['/Subtype']=='/Image' and obj['/ColorSpace']=='/DeviceRGB'
                assert (int(obj['/Width']),int(obj['/Height']))==(32,32),'authored image dimensions'
                assert obj.get_data()==rgb,'authored image RGB samples'
                maskref=obj.raw_get('/SMask'); mask=maskref.get_object(); mask_ids.add(maskref.idnum)
                assert (int(mask['/Width']),int(mask['/Height']))==(32,32),'authored mask dimensions'
                assert mask['/ColorSpace']=='/DeviceGray' and mask.get_data()==rgba[3::4],'authored alpha samples'
    # Exact multiset excludes unexpected commands, duplicate short cells, lost runs and wrong pages.
    actual_keys=sorted((p,v) for p,v,_,_ in all_text)
    expected_keys=sorted((e['page'],e['value']) for e in case['text'])
    assert actual_keys==expected_keys, 'token multiplicity/page assignment differs'
    for expected in case['text']:
        found=[(x,y) for p,v,x,y in all_text if p==expected['page'] and v==expected['value']]
        assert len(found)==1,'token multiplicity/page assignment differs'
        assert abs(found[0][0]-expected['x']) <= .002 and abs(found[0][1]-expected['top']) <= .002, (
            f'text geometry {expected["value"]}: {found[0]} != {(expected["x"],expected["top"])}')
    for cell in {e['cell'] for e in case['text']}:
        source=[(e['page'],e['value']) for e in case['text'] if e['cell']==cell]
        cell_values={v for _,v in source}
        actual=[(p,v) for p,v,_,_ in all_text if v in cell_values]
        assert actual==source,f'per-cell source order differs: {cell}'
    expected_placements=[tuple(e[k] for k in ('page','x','top','width','height')) for e in case['images']]
    assert len(placements)==len(expected_placements),'image draw multiplicity differs'
    for actual,expected in zip(placements,expected_placements):
        assert all(abs(a-b)<=.002 for a,b in zip(actual,expected)),f'image geometry {actual} != {expected}'
    assert len(image_ids)==len(mask_ids)==(1 if placements else 0),'image/mask identity differs'
    # Source ordering includes each image among text units, separately for its cell.
    if case['images']:
        for image in case['images']:
            before=[e for e in case['text'] if e['cell'] in ('left','span') and e['value']!='TAIL' and e['value']!='END']
            after=[e for e in case['text'] if e['value'] in ('TAIL','END')]
            assert all(e['page']<image['page'] or e['page']==image['page'] and e['top']<image['top'] for e in before), 'text/image source order differs'
            assert all(e['page']>image['page'] or e['page']==image['page'] and e['top']>image['top']+image['height'] for e in after), 'image/text source order differs'
    if raster:
        for page in range(case['pages']):
            prefix=path.parent/(path.stem+f'-page-{page+1}')
            run(['pdftoppm','-f',page+1,'-l',page+1,'-singlefile','-png','-r','288',path,prefix])
            with Image.open(prefix.with_suffix('.png')) as raw:
                pixels=raw.convert('RGB')
                def pixel(x,top,color,reason):
                    nonlocal checked
                    actual=pixels.getpixel((int(x*4),int(top*4)))
                    assert max(abs(a-b) for a,b in zip(actual,color))<=2,f'{reason}: raster color {(x,top)} {actual} != {color}'
                    checked+=1
                for witness in case['witnesses']:
                    if witness['page']==page:
                        pixel(witness['x'],witness['top'],witness['color'],witness['reason'])
                for image in case['images']:
                    if image['page']!=page: continue
                    # Quadrant-interior centers avoid sampling interpolation at alpha transitions.
                    for sx,sy in [(8,8),(24,8),(8,24),(24,24)]:
                        at=(sy*32+sx)*4; alpha=rgba[at+3]
                        color=[(c*alpha+bg*(255-alpha)+127)//255 for c,bg in zip(rgba[at:at+3],image['background'])]
                        pixel(image['x']+(sx+.5)*image['width']/32,image['top']+(sy+.5)*image['height']/32,color,'image alpha composition')
                    pixel(image['x']-1,image['top']+image['height']/2,CELL,'image padding')
                    pixel(image['x']-2.5,image['top']+image['height']/2,IMAGE_BORDER,'complete atomic image border')
                    pixel(image['x']+image['width']+2.5,image['top']+image['height']/2,IMAGE_BORDER,'complete atomic image border')
                    pixel(image['x']+image['width']/2,image['top']-2.5,IMAGE_BORDER,'complete atomic image border')
                    pixel(image['x']+image['width']/2,image['top']+image['height']+2.5,IMAGE_BORDER,'complete atomic image border')
    if case.get('rectangles') is not None:
        remaining=list(rectangles)
        for expected in case['rectangles']:
            geometry=[expected[k] for k in ('page','x','top','width','height')]
            match=next((i for i,actual in enumerate(remaining)
                        if all(abs(a-b)<=.002 for a,b in zip(actual[:5],geometry))
                        and all(abs(a-b/255)<=.00051 for a,b in zip(actual[5],expected['color']))),None)
            assert match is not None,f'authored rectangle geometry missing: {expected}'
            remaining.pop(match)
        assert not remaining,f'unexpected rectangle geometry: {remaining}'
    if case.get('commands') is not None:
        assert len(all_text)+len(rectangles)+len(placements)==case['commands'],'actual PDF command count differs'
    if qpdf: run([qpdf,'--check',path])
    return {'name':case['name'],'pages':len(reader.pages),'tokens':len(all_text),'imageDraws':len(placements),
            'uniqueImages':len(image_ids),'uniqueMasks':len(mask_ids),'rasterWitnesses':checked,'rectangles':len(rectangles),
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}


def reject_mutations(output, manifest):
    evidence=[]
    mutations=[('omit-page2-line','S1','token multiplicity/page assignment'),
               ('displace-line-1pt','S1','text geometry'),
               ('duplicate-short-cell','S1','token multiplicity/page assignment'),
               ('horizontal-cut','S1','no internal top cut'),
               ('terminal-bottom','S1','terminal bottom edge'),
               ('move-image','S3','image geometry'),('duplicate-image','S3','image draw multiplicity'),
               ('trim-initial-gap','S7','text geometry A')]
    for name,case_name,intended in mutations:
        case=next(c for c in manifest['cases'] if c['name']==case_name)
        source=output/(case_name+'-api.pdf')
        control=output/(case_name+'-rewrite-control.pdf')
        PdfWriter(clone_from=source).write(control)
        inspect(control,case,manifest['image'])
        writer=PdfWriter(clone_from=source)
        page_index=2 if name=='terminal-bottom' else 1
        page=writer.pages[page_index]; stream=ContentStream(page.get_contents(),writer)
        if name=='omit-page2-line':
            stream.operations=[(a,o) for a,o in stream.operations if not(o==b'Tj' and str(a[0])=='L06')]
        elif name in ('displace-line-1pt','trim-initial-gap'):
            token='L06' if name=='displace-line-1pt' else 'A'
            for i,(a,o) in enumerate(stream.operations):
                if o==b'Tj' and str(a[0])==token:
                    j=next(j for j in range(i-1,-1,-1) if stream.operations[j][1]==b'Tm')
                    stream.operations[j][0][5]=FloatObject(float(stream.operations[j][0][5])+(70 if token=='A' else 1))
                    break
        elif name=='duplicate-short-cell':
            stream.operations += [([],b'BT'),([FloatObject(v) for v in (1,0,0,1,11,67)],b'Tm'),([TextStringObject('ID')],b'Tj'),([],b'ET')]
        elif name in ('horizontal-cut','terminal-bottom'):
            color=BORDER if name=='horizontal-cut' else CELL
            top=24 if name=='horizontal-cut' else 48
            stream.operations += [([],b'q'),([FloatObject(v/255) for v in color],b'rg'),
                                  ([FloatObject(v) for v in (51,100-top-1,118,1)],b're'),([],b'f'),([],b'Q')]
        elif name=='move-image':
            for i,(a,o) in enumerate(stream.operations):
                if o==b'Do':
                    j=next(j for j in range(i-1,-1,-1) if stream.operations[j][1]==b'cm')
                    stream.operations[j][0][4]=FloatObject(float(stream.operations[j][0][4])+1)
                    break
        else:
            i=next(i for i,(_,o) in enumerate(stream.operations) if o==b'Do')
            stream.operations.insert(i+1,copy.deepcopy(stream.operations[i]))
        page.replace_contents(stream); target=output/('mutation-'+name+'.pdf'); writer.write(target)
        try: inspect(target,case,manifest['image'])
        except AssertionError as error:
            assert intended in str(error),f'{name}: unrelated failure: {error}'
            evidence.append({'name':name,'rejectedBy':str(error),'rewriteControlAccepted':True})
        else: raise AssertionError('Oracle accepted actual-PDF mutation '+name)
    return evidence


def inspect_example(path, *, qpdf=None):
    """Content/integration evidence; this does not invent a visual reference."""
    reader=PdfReader(path,strict=True)
    assert len(reader.pages)>=2,'long-description sample must demonstrate continuation'
    page_text=[]; images=[]
    for n,page in enumerate(reader.pages):
        tokens=[str(a[0]) for a,o in ContentStream(page.get_contents(),reader).operations if o==b'Tj']
        page_text.append(' '.join(tokens))
        for a,o in ContentStream(page.get_contents(),reader).operations:
            if o==b'Do': images.append(page['/Resources']['/XObject'].raw_get(a[0]).idnum)
    complete=' '.join(page_text)
    for token in ('SRV-001','SRV-002','600.00','300.00','900.00','FINAL REVIEW NOTE:'):
        assert complete.count(token)==1,f'example token missing/repeated: {token}'
    notes=re.findall(r'(?<!\d)(\d{2})\. ',complete)
    assert notes==[f'{i:02}' for i in range(1,25)],'example description note loss/repetition/source order'
    assert len(images)==2 and len(set(images))==1,'example image draws/identity'
    last_description=next(i for i,t in enumerate(page_text) if 'FINAL REVIEW NOTE:' in t)
    following=next(i for i,t in enumerate(page_text) if 'SRV-002' in t)
    totals=next(i for i,t in enumerate(page_text) if '900.00' in t)
    assert last_description<=following<=totals,'example whole/split/following-row ordering'
    if qpdf: run([qpdf,'--check',path])
    run(['pdftoppm','-png','-r','110',path,Path(path).with_suffix('')])
    return {'pages':len(reader.pages),'orderedNumberedNotes':24,'imageDraws':2,'uniqueImages':1,
            'lastDescriptionPage':last_description+1,'followingRowPage':following+1,'totalsPage':totals+1,
            'referenceApproval':'pending-user-review','sha256':hashlib.sha256(Path(path).read_bytes()).hexdigest()}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--dotnet')
    parser.add_argument('--cli-dll',type=Path)
    parser.add_argument('--api-dll',type=Path)
    parser.add_argument('--native-cli',type=Path)
    parser.add_argument('--qpdf')
    parser.add_argument('--baseline',type=Path,help='Optional local captured legacy manifest; never required by CI')
    parser.add_argument('--example',type=Path,help='Optional business long-description integration sample')
    parser.add_argument('--create-only',action='store_true')
    args=parser.parse_args(); output=args.output.resolve(); output.mkdir(parents=True,exist_ok=True)
    fixtures=output/'fixtures'; manifest=create(fixtures)
    if args.create_only:
        print(f'Authored {len(manifest["cases"])} literal cases at {fixtures}'); return
    assert args.dotnet and args.cli_dll and args.api_dll,'Supply actual public API and CLI binaries'
    commands=[('managed',[args.dotnet,args.cli_dll.resolve()])]
    if args.native_cli: commands.append(('native',[args.native_cli.resolve()]))
    report={'sourceHead':run(['git','rev-parse','HEAD']).decode().strip(),
            'dirty':bool(run(['git','status','--porcelain']).strip()),'base':manifest['base'],
            'expectationsSha256':hashlib.sha256((fixtures/'table-row-fragments.json').read_bytes()).hexdigest(),
            'tools':{'pypdf':pypdf.__version__,'Pillow':PIL.__version__,'dotnet':run([args.dotnet,'--version']).decode().strip()},
            'binarySha256':{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in
                            [args.cli_dll,args.api_dll,args.cli_dll.parent/'FactsPDF.dll',args.api_dll.parent/'FactsPDF.dll']
                            +([args.native_cli] if args.native_cli else [])},
            'cases':[],'failures':[],'referenceApproval':'pending-user-review'}
    try:
        run([args.dotnet,args.api_dll.resolve(),fixtures,output])
        report['api']=json.loads((output/'api-results.json').read_text())
        for case in manifest['cases']:
            if case['entry']=='api':
                report['cases'].append({'entry':'api',**inspect(output/(case['name']+'-api.pdf'),case,manifest['image'],qpdf=args.qpdf)})
                continue
            api_target=output/(case['name']+'-api.pdf')
            report['cases'].append({'entry':'api',**inspect(api_target,case,manifest['image'],qpdf=args.qpdf)})
            previous=api_target.read_bytes()
            for entry,command in commands:
                target=output/(case['name']+'-'+entry+'.pdf')
                run([*command,fixtures/(case['name']+'.html'),target,'--overwrite','--image',f'sample={fixtures/"sample.png"}'])
                report['cases'].append({'entry':entry,**inspect(target,case,manifest['image'],qpdf=args.qpdf)})
                assert target.read_bytes()==previous,'public API/CLI/native bytes differ'
                previous=target.read_bytes()
        report['mutations']=reject_mutations(output,manifest)
        negative=next(e for e in manifest['errors'] if e['name']=='a4-atomic-line')
        for entry,command in commands:
            for existing in (False,True):
                target=output/f'negative-{entry}-{existing}.pdf'; target.unlink(missing_ok=True)
                if existing: target.write_bytes(b'KEEP ORIGINAL')
                failed=subprocess.run([str(v) for v in [*command,fixtures/(negative['name']+'.html'),target,'--overwrite']],capture_output=True,timeout=120)
                assert failed.returncode==3 and negative['code'].encode() in failed.stderr and not failed.stdout,'wrong atomic CLI diagnostic'
                assert target.read_bytes()==b'KEEP ORIGINAL' if existing else not target.exists(),'failure changed CLI destination'
                run([*command,fixtures/'A1.html',target,'--overwrite'])
                assert target.read_bytes()==(output/('A1-'+entry+'.pdf')).read_bytes(),'CLI failure/recovery differs'
                report['failures'].append({'entry':entry,'existing':existing,'preserved':True,'recoveryByteEquality':True})
        if args.baseline:
            baseline=json.loads(args.baseline.read_text()); root=args.baseline.parent.resolve()
            report['legacyBaseline']=[]
            for item in baseline['cases']:
                target=output/('legacy-'+item['name']+'.pdf')
                # Captures may bind several fixed resource names; retain every binding.
                images=item.get('images') or ({'sample':item['image']} if item.get('image') else {})
                flags=[v for name,file in images.items() for v in ('--image',f'{name}={root/file}')]
                run([*commands[0][1],root/item['html'],target,'--overwrite',*flags])
                assert target.read_bytes()==(root/item['pdf']).read_bytes(),f'legacy bytes changed: {item["name"]}'
                assert hashlib.sha256(target.read_bytes()).hexdigest()==item['sha256'],'captured hash changed'
                report['legacyBaseline'].append({'name':item['name'],'byteEquality':True,'sha256':item['sha256']})
        if args.example:
            from png_fixtures import create as create_png
            assets=output/'example-assets';create_png(assets)
            previous=None
            for entry,command in commands:
                target=output/('business-table-long-description-'+entry+'.pdf')
                run([*command,args.example.resolve(),target,'--overwrite','--image',f'logo={assets/"brand-mark.png"}'])
                if previous is not None: assert target.read_bytes()==previous,'example native/CLI bytes differ'
                previous=target.read_bytes()
                report.setdefault('example',[]).append({'entry':entry,**inspect_example(target,qpdf=args.qpdf)})
        report['status']='passed'
    except Exception as error:
        report['status']='failed'; report['failure']=str(error); raise
    finally:
        (output/'results.json').write_text(json.dumps(report,indent=2)+'\n')
    print(f'Table row fragments: {len(report["cases"])} actual PDFs; literal geometry, alpha, mutation controls and recovery passed.')


if __name__=='__main__': main()
