"""Independent PDF inspection and fail-closed, human-reviewed reference checks."""
from __future__ import annotations
import argparse
import json
import math
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET
from acceptance_corpus import digest, inside, load_manifest, write_json
from acceptance_run import run_process


def normalized_text(text: str) -> str:
    # Only layout ASCII whitespace. Preserve punctuation, NBSP and English
    # word separators. The synthetic corpus has no intended Han/Han spaces.
    text = re.sub(r'[ \t\r\n\f]+', ' ', text).strip()
    return re.sub(r'(?<=[\u3400-\u9fff]) (?=[\u3400-\u9fff])', '', text)


def check_text(text: str, case: dict) -> None:
    expected = ' '.join(case['checks']['text_blocks'])
    if normalized_text(text) != normalized_text(expected):
        raise ValueError('Independent source text differs from extracted PDF: ' + case['id'])
    if re.findall(r'\bR[0-9]{4}\b', text) != case['records']:
        raise ValueError('Missing, duplicated or reordered synthetic record IDs')


def check_boxes(xml: bytes, margin: float) -> list[dict]:
    pages = []
    for page in ET.fromstring(xml).iter():
        if page.tag.rsplit('}',1)[-1] != 'page': continue
        width = float(page.attrib['width']); height = float(page.attrib['height'])
        if not all(math.isfinite(v) and v > 0 for v in (width,height)):
            raise ValueError('Invalid page dimensions')
        boxes = []
        for word in page.iter():
            if word.tag.rsplit('}',1)[-1] != 'word': continue
            x0,y0,x1,y1 = (float(word.attrib[k]) for k in ('xMin','yMin','xMax','yMax'))
            if not all(math.isfinite(v) for v in (x0,y0,x1,y1)):
                raise ValueError('Nonfinite word geometry')
            if not (margin-0.5 <= x0 <= x1 <= width-margin+0.5 and margin-0.5 <= y0 <= y1 <= height-margin+0.5):
                raise ValueError('Text extends outside the usable page rectangle')
            boxes.append({'text': ''.join(word.itertext()), 'x0':x0,'y0':y0,'x1':x1,'y1':y1})
        pages.append({'width':width,'height':height,'words':boxes})
    if not pages: raise ValueError('No PDF pages found by the independent inspector')
    return pages


def tool(argv: list[str], output: Path) -> bytes:
    p = run_process(argv, output, 120)
    if p['returncode'] != 0 or p['timed_out'] or p['error']:
        detail = Path(p['stderr_path']).read_text(encoding='utf-8', errors='replace')[:3000]
        raise RuntimeError(f'{Path(argv[0]).name} failed independent inspection (exit={p["returncode"]}, timeout={p["timed_out"]}): {detail}')
    return Path(p['stdout_path']).read_bytes()


def inspect_pdf(pdf: Path, case: dict, environment: dict, out_dir: Path) -> dict:
    if not pdf.is_file() or not pdf.read_bytes().startswith(b'%PDF-'):
        raise ValueError('Missing or invalid PDF signature')
    out_dir.mkdir(parents=True, exist_ok=False)
    tool(['qpdf','--check',str(pdf)],out_dir/'qpdf')
    info = tool(['pdfinfo',str(pdf)],out_dir/'pdfinfo').decode('utf-8')
    match = re.search(r'^Pages:\s+(\d+)\s*$', info, re.M)
    if not match: raise ValueError('Independent page count missing')
    pages = int(match.group(1))
    text = tool(['pdftotext','-raw',str(pdf),'-'],out_dir/'text').decode('utf-8', errors='strict')
    check_text(text,case)
    bbox = tool(['pdftotext','-bbox-layout',str(pdf),'-'],out_dir/'bbox')
    geometry = check_boxes(bbox,36)
    if len(geometry) != pages: raise ValueError('Independent inspectors disagree on page count')
    tool(['pdftoppm','-r','120','-png',str(pdf),str(out_dir/'page')],out_dir/'raster')
    images = sorted(out_dir.glob('page-*.png'),key=lambda p:int(p.stem.split('-')[-1]))
    if len(images) != pages: raise ValueError('Missing rendered page images')
    (out_dir/'text.txt').write_text(text,encoding='utf-8')
    write_json(out_dir/'geometry.json',geometry)
    result = {'case_id':case['id'],'input_sha256':case['input_sha256'], 'environment_id':environment['environment_id'],
              'source_sha':environment.get('source_sha'), 'font_inputs':environment['fonts'],
              'pdf_sha256':digest(pdf.read_bytes()),'pdf_bytes':pdf.stat().st_size,'pages':pages,
              'text_sha256':digest(text.encode()),'page_image_sha256':[digest(p.read_bytes()) for p in images],
              'checks_passed':True,'checks':{'qpdf':True,'source_text':True,'record_order':True,'page_bounds':True,'all_pages_rasterized':True},
              'review_status':'pending-review'}
    write_json(out_dir/'inspection.json',result)
    return result


def validate_reference(review: dict, case: dict, environment: dict) -> None:
    if review.get('approval') != 'approved' or review.get('reviewer_kind') != 'human':
        raise ValueError('Missing genuine human page review; capture is not approval')
    if not all(review.get(k) for k in ('reviewer','review_url','reviewed_at','artifact_location')):
        raise ValueError('Review provenance is incomplete')
    if not str(review['review_url']).startswith('https://'):
        raise ValueError('Review source must be traceable')
    if review.get('case_id') != case['id'] or review.get('input_sha256') != case['input_sha256']:
        raise ValueError('Review is for a different input')
    if review.get('environment_id') != environment['environment_id'] or review.get('font_inputs') != environment['fonts']:
        raise ValueError('Environment/font drift requires separate review')
    if not set(case['requirements']).issubset(review.get('requirements',[])):
        raise ValueError('The target does not have every required assertion reviewed')
    for key,size in [('source_sha',40),('pdf_sha256',64),('text_sha256',64)]:
        if not isinstance(review.get(key),str) or not re.fullmatch('[a-f0-9]{'+str(size)+'}',review[key]):
            raise ValueError('Incomplete fingerprint: '+key)
    if type(review.get('pages')) is not int or review['pages'] <= 0 or len(review.get('page_image_sha256',[])) != review['pages']:
        raise ValueError('Incomplete page-by-page fingerprints')
    if any(not re.fullmatch(r'[a-f0-9]{64}', h) for h in review['page_image_sha256']):
        raise ValueError('Invalid raster fingerprint')
    location = review['artifact_location']
    if '://' in location or '?' in location:
        raise ValueError('Store a durable repository-relative reference, not a temporary signed URL')
    inside(Path('.'),location)


def compare_evidence(reference: dict, candidate: dict, case: dict) -> dict:
    if not candidate.get('checks_passed'): raise ValueError('Independent checks did not pass')
    for key in ('pdf_sha256','text_sha256','pages','page_image_sha256'):
        if reference.get(key) != candidate.get(key): raise ValueError('Reference regression: '+key)
    return {'case_id':case['id'],'matched':True}


def chrome_reference(source: Path, case: dict, fonts: list[Path], environment: dict, out_dir: Path) -> dict:
    chrome = shutil.which('google-chrome') or shutil.which('chromium') or shutil.which('chromium-browser')
    if not chrome: raise RuntimeError('Independent Chrome executable missing')
    out_dir.mkdir(parents=True,exist_ok=False)
    faces = ''.join('@font-face{font-family:Acceptance'+str(i)+';src:url("'+p.resolve().as_uri()+'");font-weight:normal;}' for i,p in enumerate(fonts))
    family = ','.join('Acceptance'+str(i) for i in range(len(fonts))) or 'Courier'
    # Explicitly documented print-only normalization: no browser furniture,
    # same page/margins and supplied fonts, no synthetic heading bold.
    css = faces + '@page{size:595.28pt 841.89pt;margin:36pt}html,body{margin:0;padding:0}body{font-family:'+family+'}h1,h2{font-weight:normal}*{print-color-adjust:exact!important;-webkit-print-color-adjust:exact!important;font-kerning:none!important;font-variant-ligatures:none!important}'
    original = source.read_text(encoding='utf-8')
    normalized = original.replace('</head>','<style>'+css+'</style></head>')
    html = out_dir/'chrome-input.html'; html.write_text(normalized,encoding='utf-8')
    pdf = out_dir/'chrome.pdf'
    args = [chrome,'--headless=new','--disable-gpu','--no-sandbox','--disable-dev-shm-usage','--disable-background-networking',
            '--disable-extensions','--no-first-run','--no-default-browser-check',
            '--no-pdf-header-footer','--allow-file-access-from-files','--user-data-dir='+str((out_dir/'profile').resolve()),
            '--print-to-pdf='+str(pdf.resolve()),html.resolve().as_uri()]
    tool(args,out_dir/'print')
    result = inspect_pdf(pdf,case,environment,out_dir/'inspection')
    result.update(original_input_sha256=digest(source.read_bytes()), normalized_input_sha256=digest(html.read_bytes()),
                  role='independent-print-reference-not-the-FactsPDF-output')
    write_json(out_dir/'comparison-source.json',result)
    return result


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('mode',choices=['capture','verify']); p.add_argument('--manifest',type=Path,required=True)
    p.add_argument('--runs',type=Path,required=True); p.add_argument('--output',type=Path,required=True)
    p.add_argument('--reviews',type=Path)
    a=p.parse_args(); m=load_manifest(a.manifest)
    env=json.loads((a.runs/'environment.json').read_text())
    records=json.loads((a.runs/'runs.json').read_text())['runs']
    # The standalone inspector must not approve a partial run collection.
    # Import here to keep lower-level inspection independent of orchestration.
    from acceptance_pipeline import review_map, check_entrypoints, _stored_reference
    document={'schema_version':1,'reviews':[]} if not a.reviews else json.loads(a.reviews.read_text())
    reviews=review_map(document,m,required=a.mode=='verify')
    known={c['id']:c for c in m['cases']}; seen=set()
    for row in records:
        key=(row.get('case_id'),row.get('entrypoint'))
        if key in seen or key[0] not in known or key[1] not in known[key[0]]['entrypoints']:
            raise ValueError('Unknown, repeated or inapplicable inspection record')
        seen.add(key); case=known[key[0]]
        expected_fonts=[{'name':f['name'],'sha256':f['sha256']} for f in env['fonts']] if case['font_roles'] else []
        if (row.get('source_sha') != env.get('source_sha') or
            row.get('environment_id') != env['environment_id'] or
            row.get('input_sha256') != case['input_sha256'] or row.get('font_inputs') != expected_fonts):
            raise ValueError('Inspection record provenance differs from its input/environment')
    for case in m['cases']:
        if case['kind']=='baseline':
            check_entrypoints(case,[r for r in records if r['case_id']==case['id']])
    results=[]
    for case in m['cases']:
        if case['kind']!='baseline':continue
        for row in [r for r in records if r['case_id']==case['id']]:
            if row['status']!='candidate': raise ValueError('Cannot inspect a failed render')
            evidence=inspect_pdf(Path(row['pdf_path']),case,env,a.output/case['id']/row['entrypoint'])
            if a.mode=='verify':
                ref=reviews[case['id']];validate_reference(ref,case,env)
                _stored_reference(ref,Path.cwd());compare_evidence(ref,evidence,case)
            results.append(evidence)
    if not results:raise ValueError('No candidate baseline outputs')
    write_json(a.output/'inspection-summary.json',{'mode':a.mode,'results':results,'review_status':'pending-review' if a.mode=='capture' else 'approved'})

if __name__=='__main__':main()
