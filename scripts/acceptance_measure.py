"""Separate cold-process and warm-conversion evidence; no approved performance claims."""
from __future__ import annotations
import argparse
import json
import math
import os
from pathlib import Path
import re
import signal
import statistics
import subprocess
import sys
import time
from acceptance_corpus import BASELINE_SHA, digest, inside, load_manifest, write_json
from acceptance_run import command_for, run_process

WARM_SCOPE = 'warm-convert-and-output-buffer-current-thread'
COLD_SCOPE = 'process-start-through-cli-exit-including-input-fonts-and-file-output'


def _integer(value, minimum=0):
    if type(value) is not int or value < minimum: raise ValueError('Missing/invalid integer sample')
    return value


def _number(value):
    if type(value) not in (int, float) or not math.isfinite(value) or value < 0:
        raise ValueError('Missing/invalid finite sample')
    return value


def _hash(value, size=64):
    if not isinstance(value, str) or not re.fullmatch('[0-9a-f]{'+str(size)+'}', value):
        raise ValueError('Missing/invalid provenance hash')


def validate_measurements(data: dict) -> dict:
    if data.get('schema_version') != 1 or data.get('engine_baseline_sha') != BASELINE_SHA:
        raise ValueError('This corpus uses the explicit M12 baseline, never the inherited M8 harness')
    for name, size in [('source_sha',40),('environment_id',64),('input_sha256',64)]: _hash(data.get(name), size)
    _number(data.get('font_load_elapsed_ms')); _integer(data.get('font_load_managed_allocated_bytes'))
    common = None
    for group, scope in [('cold', COLD_SCOPE), ('warm', WARM_SCOPE)]:
        count = _integer(data.get('expected_'+group+'_samples'), 1)
        rows = data.get(group)
        if not isinstance(rows,list) or len(rows) != count: raise ValueError('Incomplete raw sample set')
        pids = set()
        for index, row in enumerate(rows):
            if type(row.get('sample_index')) is not int or row['sample_index'] != index: raise ValueError('Sample indices are not unique and ordered')
            if row.get('measurement_scope') != scope: raise ValueError('Measurement scope mismatch')
            _number(row.get('elapsed_ms')); _hash(row.get('pdf_sha256'))
            _integer(row.get('pdf_bytes'),1); _integer(row.get('pages'),1)
            output = (row['pdf_sha256'],row['pdf_bytes'],row['pages'])
            if common is None: common = output
            if output != common: raise ValueError('Sample PDF hash/bytes/pages differ')
            if group == 'warm': _integer(row.get('managed_allocated_bytes'))
            else:
                pid = _integer(row.get('pid'),1)
                if pid in pids: raise ValueError('Cold samples did not use distinct child processes')
                pids.add(pid)
                peak = row.get('peak_rss_bytes')
                if peak is not None: _integer(peak,1)
    if data.get('warm_process_peak_bytes') is not None: _integer(data['warm_process_peak_bytes'],1)
    if not isinstance(data.get('font_inputs'),list): raise ValueError('Missing ordered font provenance')
    for font in data['font_inputs']: _hash(font.get('sha256'))
    return data


def summarize_measurements(data: dict) -> dict:
    validate_measurements(data)
    med = lambda group, key: statistics.median(row[key] for row in data[group])
    peaks = [row['peak_rss_bytes'] for row in data['cold']]
    return {'case_id': data['case_id'], 'batch_index': data['batch_index'],
            'cold_samples':len(data['cold']), 'warm_samples':len(data['warm']),
            'cold_elapsed_median_ms':med('cold','elapsed_ms'),
            'warm_elapsed_median_ms':med('warm','elapsed_ms'),
            'warm_managed_allocated_median_bytes':med('warm','managed_allocated_bytes'),
            'cold_peak_rss_median_bytes':statistics.median(peaks) if all(p is not None for p in peaks) else None,
            'warm_process_peak_bytes':data.get('warm_process_peak_bytes'),
            'pdf_sha256':data['warm'][0]['pdf_sha256'], 'pdf_bytes':data['warm'][0]['pdf_bytes'],
            'pages':data['warm'][0]['pages'], 'status':'provisional', 'budget_approved':False}


def parallel_output_paths(root: Path, count: int) -> list[Path]:
    if type(count) is not int or not 1 <= count <= 2: raise ValueError('This probe uses at most two tasks')
    return [root / f'worker-{i}' for i in range(count)]


def _cold_child(argv: list[str], work: Path, timeout: float) -> dict:
    """wait4 reports this direct CLI child's high-water RSS, not all previous children."""
    work.mkdir(parents=True, exist_ok=False)
    if not sys.platform.startswith('linux') or not hasattr(os, 'wait4'):
        result = run_process(argv, work/'process', timeout)
        result['peak_rss_bytes'] = None
        result['peak_scope'] = 'not-measured-on-this-platform'
        return result
    started = time.perf_counter()
    with (work/'stdout.bin').open('wb') as out, (work/'stderr.txt').open('wb') as err:
        with subprocess.Popen(argv, stdin=subprocess.DEVNULL, stdout=out, stderr=err, start_new_session=True) as child:
            timed_out = False
            while True:
                pid, status, usage = os.wait4(child.pid, os.WNOHANG)
                if pid: break
                if time.perf_counter()-started > timeout:
                    timed_out = True
                    try: os.killpg(child.pid, signal.SIGKILL)
                    except ProcessLookupError: pass
                    pid, status, usage = os.wait4(child.pid, 0)
                    break
                time.sleep(0.001)
            child.returncode = os.waitstatus_to_exitcode(status)
            return {'pid':pid, 'returncode':child.returncode, 'timed_out':timed_out,
                    'error':None, 'cleanup_ok':True, 'elapsed_ms':(time.perf_counter()-started)*1000,
                    'peak_rss_bytes':int(usage.ru_maxrss)*1024 if usage.ru_maxrss > 0 else None,
                    'peak_scope':'Linux-wait4-direct-cli-child-high-water-rss',
                    'stdout_path':str(work/'stdout.bin'), 'stderr_path':str(work/'stderr.txt')}


def measure_cold(executable: Path, case: dict, fonts: list[Path], repeats: int, out_dir: Path) -> list[dict]:
    if not 1 <= repeats <= 100: raise ValueError('Cold sample count outside probe limits')
    rows=[]
    source = inside(Path(case['_root']), case['input_path'])
    if digest(source.read_bytes()) != case['input_sha256']: raise ValueError('Changed measured input')
    for index in range(repeats):
        work=out_dir/f'cold-{index:03}'; work.mkdir(parents=True, exist_ok=False)
        pdf=work/'result.pdf'
        proc=_cold_child(command_for(case,'cli',executable,fonts,pdf), work/'process',120)
        if proc['returncode'] != 0 or proc['timed_out'] or proc['error']:
            raise ValueError('Cold CLI sample failed; no fabricated/imputed sample')
        content=pdf.read_bytes()
        if not content.startswith(b'%PDF-'): raise ValueError('Cold sample output is not PDF')
        info=subprocess.run(['pdfinfo',str(pdf)],capture_output=True,timeout=30,check=True).stdout.decode()
        match=re.search(r'^Pages:\s+(\d+)$',info,re.M)
        if not match: raise ValueError('Independent page count unavailable')
        row={'sample_index':index,'pid':proc['pid'],'elapsed_ms':proc['elapsed_ms'],
             'peak_rss_bytes':proc['peak_rss_bytes'],'pdf_bytes':len(content),
             'pdf_sha256':digest(content),'pages':int(match.group(1)), 'measurement_scope':COLD_SCOPE,
             'peak_scope':proc['peak_scope'], 'os_file_cache':'not-flushed',
             'managed_allocated_bytes':None}
        write_json(work/'sample.json',row); rows.append(row)
    return rows


def collect(manifest_path: Path, cli: Path, host: Path, fonts: list[Path], environment: dict,
            out_dir: Path, cold_samples=10, warm_samples=30, batches=2) -> dict:
    if not (1 <= batches <= 5 and 1 <= cold_samples <= 100 and 1 <= warm_samples <= 1000):
        raise ValueError('Sample sizes outside bounded measurement scope')
    manifest=load_manifest(manifest_path); out_dir.mkdir(parents=True, exist_ok=False)
    summaries=[]
    for case0 in manifest['cases']:
        if case0['id'] not in {'B00','B01','B02-100'}: continue
        case={**case0,'_root':manifest_path.parent}; selected=fonts if case['font_roles'] else []
        for batch in range(batches):
            work=out_dir/case['id']/f'batch-{batch}'; work.mkdir(parents=True)
            cold=measure_cold(cli,case,selected,cold_samples,work)
            command=[str(host.resolve()),'measure',str(inside(manifest_path.parent,case['input_path'])),
                     '--samples',str(warm_samples),'--warmups','3']
            if selected: command+=['--subset-fonts']
            for font in selected: command+=['--font',str(font.resolve())]
            process=run_process(command,work/'warm-process',180)
            if process['returncode'] != 0 or process['timed_out'] or process['error']:
                raise ValueError('Warm measurement host failed')
            raw=json.loads(Path(process['stdout_path']).read_bytes())
            if raw.get('status') != 'measured' or raw.get('source_sha') != environment['source_sha']:
                raise ValueError('Warm host measurement provenance mismatch')
            data={'schema_version':1,'engine_baseline_sha':BASELINE_SHA,'source_sha':environment['source_sha'],
                  'environment_id':environment['environment_id'],'case_id':case['id'],'batch_index':batch,
                  'input_sha256':case['input_sha256'],'font_inputs':[{'name':p.name,'sha256':digest(p.read_bytes())} for p in selected],
                  'status':'provisional','expected_cold_samples':cold_samples,'expected_warm_samples':warm_samples,
                  'cold':cold,'warm':raw['samples'],'warm_process_peak_bytes':raw.get('warm_process_peak_bytes'),
                  'font_load_elapsed_ms':raw['font_load_elapsed_ms'],
                  'font_load_managed_allocated_bytes':raw['font_load_managed_allocated_bytes'],
                  'font_load_scope':raw['font_load_scope'],'warm_runtime':raw,
                  'reference_machine_approved':False,'page_reference_approved':False}
            validate_measurements(data); write_json(work/'raw.json',data)
            summary=summarize_measurements(data); write_json(work/'summary.json',summary); summaries.append(summary)
    report={'schema_version':1,'engine_baseline_sha':BASELINE_SHA,'source_sha':environment['source_sha'],
            'environment':environment,'batches':batches,'cases':summaries,'budget_approved':False,
            'scope':'provisional acceptance measurements, not the inherited M8 benchmark'}
    write_json(out_dir/'measurements.json',report)
    return report


def main():
    p=argparse.ArgumentParser(); p.add_argument('command',choices=['collect']); p.add_argument('--manifest',type=Path,required=True)
    p.add_argument('--native-cli',type=Path,required=True); p.add_argument('--native-host',type=Path,required=True)
    p.add_argument('--font',type=Path,action='append',default=[]); p.add_argument('--environment',type=Path,required=True)
    p.add_argument('--cold-samples',type=int,default=10); p.add_argument('--warm-samples',type=int,default=30)
    p.add_argument('--batches',type=int,default=2); p.add_argument('--output',type=Path,required=True)
    a=p.parse_args(); collect(a.manifest,a.native_cli,a.native_host,a.font,json.loads(a.environment.read_text()),a.output,
                             a.cold_samples,a.warm_samples,a.batches)

if __name__=='__main__': main()
