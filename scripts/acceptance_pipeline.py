"""Acceptance orchestration: capture is evidence collection, never human approval."""
from __future__ import annotations
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re
import shutil
import subprocess
from acceptance_corpus import digest, inside, load_manifest, summarize_results, write_json
from acceptance_run import capture_environment, run_case, run_process
from acceptance_inspect import inspect_pdf, chrome_reference, validate_reference, compare_evidence
from acceptance_measure import collect, parallel_output_paths


def require_verified_targets(summary: dict) -> None:
    if summary['target_total'] != summary['target_verified_count']:
        raise ValueError('Preview target requirements are not verified')


def review_map(document: dict, manifest: dict, required=False) -> dict:
    if document.get('schema_version') != 1 or not isinstance(document.get('reviews'),list):
        raise ValueError('Invalid review document')
    known={c['id'] for c in manifest['cases']}; result={}
    for review in document['reviews']:
        ident=review.get('case_id')
        if ident not in known or ident in result: raise ValueError('Unknown or duplicate reference review')
        result[ident]=review
    if required and any(c['id'] not in result for c in manifest['cases'] if c['kind']=='baseline'):
        raise ValueError('Missing human page reviews; verify cannot fall back to capture')
    return result


def check_entrypoints(case: dict, rows: list[dict]) -> None:
    expected=set(case['entrypoints']); found=[r.get('entrypoint') for r in rows]
    if len(found)!=len(set(found)) or set(found)!=expected or any(r.get('case_id')!=case['id'] for r in rows):
        raise ValueError('Missing, duplicate or inapplicable entrypoint result')
    if case['kind']=='baseline':
        if any(r.get('status')!='candidate' for r in rows):raise ValueError('Baseline rendering failed')
        if len({(r.get('pdf_sha256'),r.get('pdf_bytes')) for r in rows})!=1:
            raise ValueError('CLI/public API output bytes disagree')


def public_copy(source: Path, destination: Path, relative: str) -> Path:
    target=inside(destination,relative)
    name=target.name
    allowed=name in {'native.pdf','chrome.pdf','text.txt','chrome-text.txt','geometry.json','inspection.json',
                     'comparison.json','environment.json','runs.json','summary.json','measurements.json',
                     'manifest.json','report.md','README.md','reference-reviews.json','report-records.json'}
    allowed |= bool(re.fullmatch(r'(?:page|chrome-page|overlay-page|difference-page)-[0-9]+\.png',name))
    allowed |= bool(re.fullmatch(r'[BTN][A-Za-z0-9-]+\.html',name))
    allowed |= bool(re.fullmatch(r'B[A-Za-z0-9-]+-batch-[0-9]+-raw\.json',name))
    if not allowed or source.is_symlink():raise ValueError('File is not in the explicit public evidence allowlist')
    if not source.is_file():raise ValueError('Public evidence file is missing')
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
    return target


def report_markdown(summary: dict, cases: list[dict]) -> str:
    observations=''.join('- '+c['case_id']+': '+c['chrome_margin_observation']+'\n' for c in cases if c.get('chrome_margin_observation'))
    return ('# FactsPDF document acceptance — candidate review pack\n\n'
            f"Source: `{summary['source_sha']}`\n\n"
            f"Independently checked baseline cases: {summary.get('baseline_independently_checked',0)}/{summary['baseline_total']}. "
            f"Human-approved frozen baselines: {summary['baseline_regression_passed']}/{summary['baseline_total']}.\n\n"
            f"Expected negative cases: {summary['negative_expected_errors']}/{summary['negative_total']}. "
            f"Verified future targets: {summary['target_verified_count']}/{summary['target_total']}.\n\n"
            'Human page approval is pending unless an exact reviewed record is supplied. Capture is NOT approval. '
            'Developer Preview 1 is NOT complete. Performance budgets and fixed reference hardware are NOT approved.\n\n'
            '| Case | Status | Independent native pages | Chrome pages |\n|---|---|---:|---:|\n'+
            ''.join(f"| {c['case_id']} | {c['status']} | {c.get('pages','—')} | {c.get('chrome_pages','—')} |\n" for c in cases)+
            '\n## Browser reference observations (NOT approved)\n\n'+(observations or 'None recorded.\n')+
            '\nNext gate: inspect every native/Chrome page, then approve or correct the reference; '
            'lists, real bold, images, tables and page furniture remain unmet product targets.\n')


def compare_images(native: Path, chrome: Path, public: Path, ident: str) -> dict:
    from PIL import Image, ImageChops, ImageStat
    left=sorted(native.glob('page-*.png'),key=lambda p:int(p.stem.split('-')[-1]))
    right=sorted(chrome.glob('page-*.png'),key=lambda p:int(p.stem.split('-')[-1]))
    metrics=[]
    for index,(a,b) in enumerate(zip(left,right),1):
        with Image.open(a) as aa, Image.open(b) as bb:
            im=aa.convert('RGB'); other=bb.convert('RGB')
            original_native=list(im.size); original_chrome=list(other.size)
            padding='none'
            if im.size!=other.size:
                if abs(im.width-other.width)>1 or abs(im.height-other.height)>1:
                    metrics.append({'page':index,'same_dimensions':False,'native':original_native,'chrome':original_chrome});continue
                size=(max(im.width,other.width),max(im.height,other.height))
                native_canvas=Image.new('RGB',size,'white');native_canvas.paste(im,(0,0))
                chrome_canvas=Image.new('RGB',size,'white');chrome_canvas.paste(other,(0,0))
                im,other=native_canvas,chrome_canvas
                padding='white-right-bottom-only-no-registration'
            diff=ImageChops.difference(im,other)
            metrics.append({'page':index,'same_dimensions':original_native==original_chrome,
                'native':original_native,'chrome':original_chrome,'padding_policy':padding,
                'mean_absolute_pixel_difference':sum(ImageStat.Stat(diff).mean)/3})
            folder=public/ident;folder.mkdir(parents=True,exist_ok=True)
            Image.blend(im,other,0.5).save(folder/f'overlay-page-{index}.png')
            diff.save(folder/f'difference-page-{index}.png')
    return {'native_pages':len(left),'chrome_pages':len(right),'page_counts_equal':len(left)==len(right),
            'pages':metrics,'scope':'unregistered full-page evidence; no global pixel-tolerance approval'}


def _stored_reference(review: dict, repo_root: Path) -> None:
    directory=inside(repo_root,review['artifact_location'])
    if not directory.is_dir():raise ValueError('Durable reviewed reference directory is missing')
    files=[(directory/'native.pdf',review['pdf_sha256']),(directory/'text.txt',review['text_sha256'])]
    files += [(directory/f'page-{i}.png',h) for i,h in enumerate(review['page_image_sha256'],1)]
    if any(not p.is_file() or digest(p.read_bytes())!=h for p,h in files):
        raise ValueError('Stored reference artifact does not match the human review')
    for file,expected in files:
        relative=file.relative_to(repo_root.resolve()).as_posix()
        if file.is_symlink():raise ValueError('Reference artifacts must be committed regular files')
        recorded=subprocess.run(['git','-C',str(repo_root.resolve()),'cat-file','blob','HEAD:'+relative],
                                capture_output=True,timeout=10)
        if recorded.returncode != 0 or digest(recorded.stdout) != expected:
            raise ValueError('Reference artifact is not durably committed in HEAD: '+relative)


def execute(mode: str, manifest_path: Path, cli: Path, host: Path, fonts: list[Path], reviews_path: Path,
            output: Path, measure=False, strict_targets=False) -> dict:
    if mode not in {'capture','verify'}:raise ValueError('Explicit capture or verify mode required')
    manifest=load_manifest(manifest_path)
    reviews=review_map(json.loads(reviews_path.read_text()),manifest,required=mode=='verify')
    output.mkdir(parents=True,exist_ok=False)
    public=output/'public';public.mkdir()
    environment=capture_environment({'cli':cli,'api':host},fonts)
    source=subprocess.run(['git','rev-parse','HEAD'],capture_output=True,text=True,check=True).stdout.strip()
    if environment['source_sha']!=source or not re.fullmatch('[a-f0-9]{40}',source):
        raise ValueError('Claimed and actual checkout source SHA differ')
    if any(environment['tools'].get(tool) is None for tool in ('qpdf','pdfinfo','pdftotext','pdftoppm','chromium')):
        raise ValueError('Required independent inspection tools are missing')
    info=run_process([str(host.resolve()),'info'],output/'host-info',30)
    if info['returncode']!=0 or info['error'] or info['timed_out']:raise ValueError('API host info unavailable')
    runtime=json.loads(Path(info['stdout_path']).read_bytes())
    if runtime.get('source_sha')!=source or runtime.get('dynamic_code_supported') is not False:
        raise ValueError('This acceptance run requires the real Native AOT public API host')
    environment['host_runtime']=runtime
    write_json(output/'environment.json',environment)
    public_copy(output/'environment.json',public,'environment.json')
    write_json(public/'summary.json',{'source_sha':source,'status':'incomplete-capture','work_package_complete':False,'preview_ready':False})
    all_runs=[];case_results=[];checked=0;native_rows={}
    for original in manifest['cases']:
        case={**original,'_root':manifest_path.parent,'_source_sha':source,
              '_environment_id':environment['environment_id'],'_build_mode':'linux-x64-native-aot'}
        if case['input_path'] is None:
            case_results.append({'case_id':case['id'],'status':'blocked-design'});continue
        selected=fonts if case['font_roles'] else []
        rows=[run_case(case,ep,cli if ep=='cli' else host,selected,output/'runs',120) for ep in case['entrypoints']]
        all_runs+=rows
        print('Captured '+case['id']+': '+', '.join(r['entrypoint']+'='+r['status'] for r in rows),flush=True)
        write_json(output/'runs.json',{'source_sha':source,'runs':all_runs,'status':'incomplete-capture'})
        public_copy(output/'runs.json',public,'runs.json')
        check_entrypoints(case,rows)
        if case['kind']=='negative':
            if any(r['status']!='expected-error' for r in rows):raise ValueError('Negative boundary failed: '+case['id']+' '+str(rows))
            case_results.append({'case_id':case['id'],'status':'expected-error'});continue
        if case['kind']=='target':
            if any(r['status']=='infrastructure-error' for r in rows):raise ValueError('Target probe infrastructure failed: '+case['id'])
            state='unsupported' if all(r['status']=='unsupported' for r in rows) else 'candidate'
            case_results.append({'case_id':case['id'],'status':state});continue
        native=next(r for r in rows if r['entrypoint']=='cli');native_rows[case['id']]=native
        evidence=None
        for r in rows:
            item=inspect_pdf(Path(r['pdf_path']),case,environment,output/'inspection'/case['id']/r['entrypoint'])
            if evidence is None:evidence=item
            elif any(evidence[k]!=item[k] for k in ('pdf_sha256','text_sha256','pages','page_image_sha256')):
                raise ValueError('Entrypoint inspection mismatch')
        public_copy(Path(native['pdf_path']),public,case['id']+'/native.pdf')
        for name in ('text.txt','geometry.json','inspection.json'):
            public_copy(output/'inspection'/case['id']/'cli'/name,public,case['id']+'/'+name)
        for image in (output/'inspection'/case['id']/'cli').glob('page-*.png'):
            public_copy(image,public,case['id']+'/'+image.name)
        reference=chrome_reference(inside(manifest_path.parent,case['input_path']),case,selected,environment,output/'chrome'/case['id'])
        row={'case_id':case['id'],'status':'pending-review','pages':evidence['pages'],'chrome_pages':reference['pages'],
             'inspection_passed':True,'evidence_sha256':digest(json.dumps(evidence,sort_keys=True).encode())}
        if mode=='verify':
            approved=reviews[case['id']];validate_reference(approved,case,environment)
            _stored_reference(approved,Path.cwd());compare_evidence(approved,evidence,case)
            row.update(status='passed',review_status='approved',verified_requirements=case['requirements'])
        checked+=1;case_results.append(row)
        folder=output/'inspection'/case['id']/'cli';chrome=output/'chrome'/case['id']/'inspection'
        public_copy(Path(native['pdf_path']),public,case['id']+'/native.pdf')
        public_copy(output/'chrome'/case['id']/'chrome.pdf',public,case['id']+'/chrome.pdf')
        for name in ('text.txt','geometry.json','inspection.json'):public_copy(folder/name,public,case['id']+'/'+name)
        public_copy(chrome/'text.txt',public,case['id']+'/chrome-text.txt')
        for prefix,directory in [('',folder),('chrome-',chrome)]:
            for image in directory.glob('page-*.png'):public_copy(image,public,case['id']+'/'+prefix+image.name)
        comparison=compare_images(folder,chrome,public,case['id'])
        comparison['native_checks']=evidence['checks'];comparison['chrome_checks']=reference['checks']
        comparison['chrome_margin_observation']=reference.get('reference_margin_observation')
        row['chrome_margin_observation']=reference.get('reference_margin_observation')
        comparison['chrome_original_input_sha256']=reference['original_input_sha256']
        comparison['chrome_normalized_input_sha256']=reference['normalized_input_sha256']
        write_json(public/case['id']/'comparison.json',comparison)
    b00=next(c for c in manifest['cases'] if c['id']=='B00')
    base={**b00,'_root':manifest_path.parent,'_source_sha':source,'_environment_id':environment['environment_id'],'_build_mode':'linux-x64-native-aot'}
    stdout=run_case({**base,'_stdout':True},'cli',cli,[],output/'binary-stdout',120)
    if stdout['status']!='candidate' or stdout['pdf_sha256']!=native_rows['B00']['pdf_sha256']:
        raise ValueError('Binary stdout did not preserve PDF bytes')
    cancelled={**base,'kind':'negative','_pre_cancelled':True,'entrypoints':{'api':{'expected_exit':130,'expected_code':None}}}
    cancellation=run_case(cancelled,'api',host,[],output/'cancellation',120)
    if cancellation['status']!='expected-error':raise ValueError('API cancellation damaged output')
    paths=parallel_output_paths(output/'parallel',2)
    with ThreadPoolExecutor(max_workers=2) as pool:
        futures=[pool.submit(run_case,base,'cli',cli,[],path,120) for path in paths]
        parallel=[f.result() for f in futures]
    if any(r['status']!='candidate' or r['pdf_sha256']!=native_rows['B00']['pdf_sha256'] for r in parallel):
        raise ValueError('Parallel outputs are not independent/identical')
    write_json(output/'runs.json',{'source_sha':source,'runs':all_runs,'binary_stdout':stdout,'cancellation':cancellation,'parallel':parallel})
    summary=summarize_results(manifest,case_results)
    summary.update(source_sha=source,mode=mode,baseline_independently_checked=checked,
                   page_review_status='pending-user-page-review' if mode=='capture' else 'approved',
                   reference_machine_approved=False,performance_budget_approved=False,work_package_complete=False,
                   cases=case_results)
    write_json(output/'summary.json',summary)
    if measure:
        measured=collect(manifest_path,cli,host,fonts,environment,output/'measurements')
        public_copy(output/'measurements'/'measurements.json',public,'measurements.json')
        for raw in (output/'measurements').glob('*/batch-*/raw.json'):
            public_copy(raw,public,f'{raw.parent.parent.name}-{raw.parent.name}-raw.json')
    for name in ('environment.json','runs.json','summary.json'):public_copy(output/name,public,name)
    public_copy(manifest_path,public,'manifest.json')
    public_copy(reviews_path,public,'reference-reviews.json')
    public_copy(manifest_path.parent/'data/report-records.json',public,'report-records.json')
    for case in manifest['cases']:
        if case['input_path']:public_copy(inside(manifest_path.parent,case['input_path']),public,'inputs/'+case['id']+'.html')
    (public/'report.md').write_text(report_markdown(summary,case_results),encoding='utf-8')
    if strict_targets:require_verified_targets(summary)
    return summary


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('mode',choices=['capture','verify'])
    p.add_argument('--manifest',type=Path,required=True);p.add_argument('--native-cli',type=Path,required=True)
    p.add_argument('--api-host',type=Path,required=True);p.add_argument('--font',action='append',type=Path,default=[])
    p.add_argument('--reviews',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--measure',action='store_true');p.add_argument('--strict-targets',action='store_true')
    a=p.parse_args()
    try:
        summary=execute(a.mode,a.manifest,a.native_cli,a.api_host,a.font,a.reviews,a.output,a.measure,a.strict_targets)
        print(json.dumps(summary,ensure_ascii=False))
    except (ValueError,RuntimeError,OSError,subprocess.SubprocessError) as ex:
        print('Acceptance evidence failed: '+str(ex),file=__import__('sys').stderr);raise SystemExit(1)

if __name__=='__main__':main()
