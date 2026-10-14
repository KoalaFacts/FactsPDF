"""Supplemental Linux peaks via a locally compiled wait4 supervisor (cc/clang).

GNU time is not required. Original cold/warm samples stay intact. This measures
kernel process-lifetime RSS after a small native supervisor forks, not engine-only
memory or a simultaneous process-tree peak. Compilation precedes probe timing.
"""
from __future__ import annotations
import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import signal
import statistics
import subprocess
import sys
import tempfile
import time

PEAK_SCOPE = 'linux-native-wait4-direct-child-lifetime-rss-after-small-supervisor-fork'
SUPERVISOR_VERSION = 'FactsPDF wait4 supervisor v1'
SUPERVISOR_SOURCE = Path(__file__).with_name('acceptance_peak_supervisor.c')
PARENTS = (0, 192)
LOADS = (0, 16, 128)
CALIBRATION_ALLOWANCE = 8 * 1024 * 1024  # Control-method allowance, NOT a product budget.


def fingerprint(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, sort_keys=True) + '\n', encoding='utf-8')


def parse_usage(text: str) -> int | None:
    match = re.fullmatch(r'peak_rss_kib=(0|[1-9][0-9]*)\nexit_code=0\n', text)
    if not match:
        raise ValueError('Failed, missing or malformed supervisor resource report')
    value = int(match.group(1))
    return value * 1024 if value else None


def _require_linux() -> None:
    if not sys.platform.startswith('linux'):
        raise RuntimeError('Linux wait4 is required; no imputed peak')


def _require_supervisor(supervisor: Path) -> Path:
    """Check local prerequisites without creating output or starting processes."""
    _require_linux()
    supervisor = supervisor.resolve()
    if not supervisor.is_file():
        raise RuntimeError(f'Native supervisor is missing or not a file: {supervisor}; no imputed peak')
    if not os.access(supervisor, os.X_OK):
        raise RuntimeError(f'Native supervisor is not executable: {supervisor}; no imputed peak')
    return supervisor


def _require_compiler(compiler: str | None = None) -> str:
    cc = shutil.which(compiler) if compiler else shutil.which('cc') or shutil.which('clang')
    if not cc: raise RuntimeError('A native compiler (cc or clang) is required; no imputed peak')
    return cc


def build_supervisor(work: Path, compiler: str | None = None) -> Path:
    """Build the bounded acceptance helper locally, without installing anything."""
    _require_linux()
    cc = _require_compiler(compiler)
    work.mkdir(parents=True, exist_ok=False)
    binary = work.resolve()/'rss-supervisor'
    subprocess.run([cc, '-O2', '-Wall', '-Wextra', '-Werror', str(SUPERVISOR_SOURCE), '-o', str(binary)],
                   check=True, timeout=30, capture_output=True)
    return _require_supervisor(binary)


@contextmanager
def _native_supervisor(supervisor: Path | None, compiler: str | None = None):
    if supervisor is not None:
        yield _require_supervisor(supervisor)
    else:
        _require_linux()
        cc = _require_compiler(compiler)
        with tempfile.TemporaryDirectory(prefix='factspdf-peak-') as temp:
            yield build_supervisor(Path(temp)/'build', cc)


def _run(argv: list[str], work: Path, timeout: float, *, join_worker_group: bool = False) -> dict:
    if not argv or timeout <= 0:
        raise ValueError('A command and a positive timeout are required')
    # Nested probes may share only a dedicated worker-owned session.
    # Never place unrelated caller processes in a timeout kill domain.
    if join_worker_group and (os.getpgrp() != os.getpid() or os.getsid(0) != os.getpid()):
        raise ValueError('Nested probe requires a dedicated worker-owned session')
    work.mkdir(parents=True, exist_ok=False)
    stdout, stderr = work/'stdout.bin', work/'stderr.txt'
    started = time.perf_counter()
    with stdout.open('wb') as out, stderr.open('wb') as err:
        with subprocess.Popen(argv, stdin=subprocess.DEVNULL, stdout=out, stderr=err,
                              start_new_session=not join_worker_group) as child:
            try:
                child.wait(timeout=timeout)
            except BaseException:
                # A nested per-probe timeout fails the whole disposable worker
                # as well. The outer launcher reaps it and reports a failed run;
                # aggregate deadlines can now terminate the exact same group.
                target_group = os.getpgrp() if join_worker_group else child.pid
                try: os.killpg(target_group, signal.SIGKILL)
                except ProcessLookupError: pass
                child.wait(timeout=5)
                raise
            if child.returncode:
                raise RuntimeError('Peak probe command failed with exit ' + str(child.returncode))
    if stdout.stat().st_size > 1024*1024 or stderr.stat().st_size > 1024*1024:
        raise RuntimeError('Peak probe exceeded diagnostic output limit')
    return {'supervisor_pid': child.pid, 'launcher_elapsed_ms': (time.perf_counter()-started)*1000,
            'stdout_path': str(stdout), 'stderr_path': str(stderr)}


def supervised(argv: list[str], work: Path, timeout: float,
               supervisor: Path | None = None, *, join_worker_group: bool = False) -> dict:
    with _native_supervisor(supervisor) as binary:
        usage = work.resolve()/'usage.txt'
        command = [str(binary), '--usage', str(usage), '--', *[str(x) for x in argv]]
        try:
            row = _run(command, work, timeout, join_worker_group=join_worker_group)
        except subprocess.TimeoutExpired as ex:
            raise TimeoutError('Peak probe timed out; its process group was killed/reaped') from ex
        row.update(peak_rss_bytes=parse_usage(usage.read_text(encoding='ascii')),
                   peak_scope=PEAK_SCOPE, missing_peak_reason=None)
        if row['peak_rss_bytes'] is None:
            row['missing_peak_reason'] = 'kernel-wait4-reported-zero; not treated as zero memory'
        return row


def validate_controls(rows: list[dict], repeats: int) -> dict:
    if type(repeats) is not int or not 1 <= repeats <= 5:
        raise ValueError('Bounded control repeat count required')
    expected = {(p, c, i) for p in PARENTS for c in LOADS for i in range(repeats)}
    keys = [(r['parent_mib'], r['child_mib'], r['sample_index']) for r in rows]
    if len(keys) != len(set(keys)) or set(keys) != expected:
        raise ValueError('Missing or duplicate calibration controls')
    for row in rows:
        peak, hwm = row.get('peak_rss_bytes'), row.get('vm_hwm_bytes')
        if type(peak) is not int or type(hwm) is not int or peak <= 0 or hwm <= 0:
            raise ValueError('Calibration requires real nonzero measurements')
        if abs(peak-hwm) > CALIBRATION_ALLOWANCE:
            raise ValueError('Supervisor peak disagrees with held-allocation post-exec VmHWM')
    med = {(p, c): statistics.median(r['peak_rss_bytes'] for r in rows
                                    if r['parent_mib'] == p and r['child_mib'] == c)
           for p in PARENTS for c in LOADS}
    for c in LOADS:
        if abs(med[(0, c)]-med[(192, c)]) > CALIBRATION_ALLOWANCE:
            raise ValueError('Peak still depends on the large Python launcher')
    for p in PARENTS:
        if med[(p, 16)]-med[(p, 0)] < 8*1024*1024 or med[(p, 128)]-med[(p, 16)] < 64*1024*1024:
            raise ValueError('Collector cannot distinguish the controlled live allocations')
    return {'parent_invariance_allowance_bytes': CALIBRATION_ALLOWANCE,
            'held_vmhwm_comparison_allowance_bytes': CALIBRATION_ALLOWANCE,
            'scope': 'measurement-method validation, not engine memory SLA'}


def _worker(job: dict, work: Path, supervisor: Path, parent_mib: int, repeats: int) -> list[dict]:
    if parent_mib not in PARENTS or not 1 <= repeats <= 5:
        raise ValueError('Invalid bounded calibration job')
    supervisor = _require_supervisor(supervisor)
    ballast = bytearray(parent_mib*1024*1024)
    for i in range(0, len(ballast), 4096): ballast[i] = 1
    rows = []
    for task in job['tasks']:
        for index in range(repeats):
            directory = work/task['id']/str(index)
            directory.mkdir(parents=True, exist_ok=False)
            pdf = directory/'result.pdf'
            argv = [str(pdf.resolve()) if x == '{output}' else x for x in task['argv']]
            # Inherit the worker's dedicated session, so its aggregate deadline
            # cannot leave this supervisor or renderer outside the kill domain.
            row = supervised(argv, directory/'process', 30, supervisor, join_worker_group=True)
            row.update(parent_mib=parent_mib, sample_index=index, case_id=task['id'])
            if job['kind'] == 'controls':
                actual = json.loads(Path(row['stdout_path']).read_bytes())
                if actual['touched_mib'] != task['child_mib']:
                    raise ValueError('Native control did not execute the requested allocation')
                row.update(child_mib=task['child_mib'], vm_hwm_bytes=actual['vm_hwm_bytes'])
            else:
                if not pdf.is_file() or fingerprint(pdf) != task['expected_pdf_sha256']:
                    raise ValueError('Peak probe altered the independently checked PDF')
                row.update(pdf_sha256=fingerprint(pdf), pdf_bytes=pdf.stat().st_size,
                           pages=task['expected_pages'], pages_source='hash-matched independently inspected candidate')
            # Public rows contain metadata, never stdout/font/binary payloads.
            row.pop('stdout_path'); row.pop('stderr_path')
            rows.append(row)
    # Keep the allocated launcher pages alive throughout all child executions.
    if ballast and ballast[0] != 1: raise RuntimeError('Launcher control was not retained')
    return rows


def _workers(job: dict, work: Path, supervisor: Path, repeats: int) -> list[dict]:
    supervisor = _require_supervisor(supervisor)
    work.mkdir(parents=True, exist_ok=False)
    job_path = work.resolve()/'job.json'
    save(job_path, job)
    rows = []
    for parent in PARENTS:
        result = _run([sys.executable, str(Path(__file__).resolve()), 'worker',
                       '--job', str(job_path), '--work', str((work/f'load-{parent}').resolve()),
                       '--supervisor', str(supervisor), '--parent-mib', str(parent),
                       '--repeats', str(repeats)], work/f'launcher-{parent}', 240)
        rows.extend(json.loads(Path(result['stdout_path']).read_bytes()))
    return rows


def calibrate(work: Path, repeats: int = 3, compiler: str | None = None,
              supervisor: Path | None = None) -> dict:
    _require_linux()
    if supervisor is not None: _require_supervisor(supervisor)
    cc = _require_compiler(compiler)
    with _native_supervisor(supervisor, cc) as binary:
        version = subprocess.check_output([str(binary), '--version'], timeout=10).decode()
        if version != SUPERVISOR_VERSION + '\n': raise ValueError('Native wait4 supervisor protocol required')
        work.mkdir(parents=True, exist_ok=False)
        source = Path(__file__).resolve().parent.parent/'tools/acceptance-rss-control.c'
        probe = work.resolve()/'rss-control'
        subprocess.run([cc, '-O2', '-Wall', '-Wextra', '-Werror', str(source), '-o', str(probe)],
                       check=True, timeout=30, capture_output=True)
        job = {'kind': 'controls', 'tasks': [{'id': str(c), 'child_mib': c,
                'argv': [str(probe), str(c)]} for c in LOADS]}
        rows = _workers(job, work/'runs', binary, repeats)
        checks = validate_controls(rows, repeats)
        report = {'schema_version': 1, 'controls_validated': True, 'budget_approved': False,
                  'peak_scope': PEAK_SCOPE, 'supervisor_version': version.splitlines()[0],
                  'supervisor_source_sha256': fingerprint(SUPERVISOR_SOURCE),
                  'supervisor_sha256': fingerprint(binary), 'control_source_sha256': fingerprint(source),
                  'control_binary_sha256': fingerprint(probe), 'platform': platform.platform(),
                  'compiler': subprocess.check_output([cc, '--version'], timeout=10).decode().splitlines()[0],
                  'repeats': repeats, 'checks': checks, 'rows': rows}
        save(work/'controls.json', report)
        return report


def collect(manifest: Path, capture: Path, cli: Path, host: Path, fonts: list[Path],
            work: Path, output: Path) -> dict:
    from acceptance_build import validate_build_provenance
    from acceptance_corpus import load_manifest, inside
    from acceptance_run import command_for
    source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    receipt = validate_build_provenance({'cli': cli, 'api': host}, source)
    env = json.loads((capture/'environment.json').read_text())
    status = json.loads((capture/'summary.json').read_text())
    if env.get('source_sha') != source or status.get('source_sha') != source or status.get('baseline_independently_checked') != 6:
        raise ValueError('Supplemental peaks require complete current-source candidate inspection')
    if env['fonts'] != [{'name': p.name, 'sha256': fingerprint(p), 'bytes': p.stat().st_size} for p in fonts]:
        raise ValueError('Peak inputs differ from the ordered candidate font fingerprints')
    m = load_manifest(manifest)
    tasks = []
    for case in m['cases']:
        if case['id'] not in {'B00', 'B01', 'B02-100'}: continue
        evidence = json.loads((capture/case['id']/'inspection.json').read_text())
        if evidence.get('source_sha') != source or evidence.get('input_sha256') != case['input_sha256'] or not evidence.get('checks_passed'):
            raise ValueError('Peak case does not have current independent output evidence')
        selected = fonts if case['font_roles'] else []
        expected_input = inside(manifest.parent, case['input_path'])
        if fingerprint(expected_input) != case['input_sha256']: raise ValueError('Input changed')
        argv = command_for({**case, '_root': manifest.parent}, 'cli', cli, selected, work/'placeholder.pdf')
        argv[2] = '{output}'
        tasks.append({'id': case['id'], 'argv': argv, 'expected_pdf_sha256': evidence['pdf_sha256'],
                      'expected_pages': evidence['pages'], 'input_sha256': case['input_sha256']})
    if len(tasks) != 3: raise ValueError('Exactly three bounded workloads are required')
    with _native_supervisor(None) as supervisor:
        controls = calibrate(work/'controls', supervisor=supervisor)
        rows = _workers({'kind': 'documents', 'tasks': tasks}, work/'documents', supervisor, 3)
    comparisons = []
    for task in tasks:
        group = [r for r in rows if r['case_id'] == task['id']]
        if len(group) != 6 or any(type(r['peak_rss_bytes']) is not int or r['peak_rss_bytes'] <= 0 for r in group):
            raise ValueError('Missing workload peak samples')
        medians = {str(p): statistics.median(r['peak_rss_bytes'] for r in group if r['parent_mib'] == p) for p in PARENTS}
        if abs(medians['0']-medians['192']) > CALIBRATION_ALLOWANCE:
            raise ValueError('Actual document peak remains launcher-sensitive')
        comparisons.append({'case_id': task['id'], 'parent_mib_to_peak_median_bytes': medians,
                            'pdf_sha256': task['expected_pdf_sha256']})
    # Never overwrite the old 60 cold/180 warm records or relabel their timing.
    report = {'schema_version': 1, 'source_sha': source, 'environment_id': env['environment_id'],
              'build_receipt': receipt, 'controls': controls, 'document_rows': rows,
              'document_comparisons': comparisons, 'peak_scope': PEAK_SCOPE,
              'status': 'calibrated-supplemental-process-peaks', 'budget_approved': False,
              'reference_machine_approved': False, 'original_measurements_changed': False,
              'limitations': ['Process peak includes loader/runtime/fonts, not engine-only allocations.',
                              'Control allowance is not a product memory budget.',
                              'launcher_elapsed_ms includes the native supervisor, excludes compilation, and is not the original cold latency.',
                              'Kernel RSS accounting and a residual small supervisor floor remain.',
                              'Only the direct executable is calibrated; not a concurrent process-tree peak.']}
    save(output, report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    worker = commands.add_parser('worker')
    for flag in ('job', 'work', 'supervisor'): worker.add_argument('--'+flag, type=Path, required=True)
    worker.add_argument('--parent-mib', type=int, required=True)
    worker.add_argument('--repeats', type=int, required=True)
    cal = commands.add_parser('calibrate'); cal.add_argument('--work', type=Path, required=True)
    run = commands.add_parser('collect')
    for flag in ('manifest', 'capture', 'cli', 'host', 'work', 'output'): run.add_argument('--'+flag, type=Path, required=True)
    run.add_argument('--font', type=Path, action='append', default=[])
    args = parser.parse_args()
    if args.command == 'worker':
        print(json.dumps(_worker(json.loads(args.job.read_text()), args.work, args.supervisor, args.parent_mib, args.repeats)))
    elif args.command == 'calibrate':
        print(json.dumps(calibrate(args.work)))
    else:
        result = collect(args.manifest, args.capture, args.cli, args.host, args.font, args.work, args.output)
        print(json.dumps({'status': result['status'], 'document_comparisons': result['document_comparisons']}))

if __name__ == '__main__': main()
