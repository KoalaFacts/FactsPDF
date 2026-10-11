"""Explicit, isolated acceptance processes. A successful PDF is not approval."""
from __future__ import annotations
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import signal
import shutil
import subprocess
import time
from acceptance_corpus import BASELINE_SHA, digest, inside, write_json

LIMITS = {'MaxPages', 'MaxInputCharacters', 'MaxElements', 'MaxDepth', 'MaxCssCharacters',
          'MaxCssDeclarations', 'MaxDisplayCommands', 'MaxOutputBytes'}


def run_process(argv: list[str], directory: Path, timeout: float) -> dict:
    if not argv or timeout <= 0:
        raise ValueError('Command and positive timeout required')
    directory.mkdir(parents=True, exist_ok=False)
    stdout = directory / 'stdout.bin'; stderr = directory / 'stderr.txt'
    started = time.perf_counter(); timed_out = False; cleanup = True
    with stdout.open('wb') as out, stderr.open('wb') as err:
        try:
            child = subprocess.Popen([str(x) for x in argv], stdout=out, stderr=err,
                                     start_new_session=os.name == 'posix')
        except OSError as exc:
            return {'pid': None, 'returncode': None, 'timed_out': False, 'cleanup_ok': True,
                    'elapsed_ms': (time.perf_counter()-started)*1000, 'error': str(exc),
                    'stdout_path': str(stdout), 'stderr_path': str(stderr)}
        try:
            child.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            try:
                if os.name == 'posix': os.killpg(child.pid, signal.SIGKILL)
                else:
                    child.kill(); cleanup = False
            except ProcessLookupError: pass
            child.wait(timeout=5)
    oversized = stdout.stat().st_size > 32*1024*1024 or stderr.stat().st_size > 1024*1024
    return {'pid': child.pid, 'returncode': child.returncode, 'timed_out': timed_out,
            'cleanup_ok': cleanup, 'elapsed_ms': (time.perf_counter()-started)*1000,
            'error': 'process output exceeds acceptance limit' if oversized else None,
            'stdout_path': str(stdout), 'stderr_path': str(stderr)}


def command_for(case: dict, entrypoint: str, executable: Path, fonts: list[Path], output: Path) -> list[str]:
    if entrypoint not in case['entrypoints']:
        raise ValueError('Entrypoint is not applicable; it is not a skipped pass')
    source = inside(Path(case['_root']), case['input_path'])
    command = [str(executable.resolve())]
    if entrypoint == 'api': command += ['render']
    elif entrypoint != 'cli': raise ValueError('Unknown entrypoint')
    command += [str(source), '-' if case.get('_stdout') else str(output.resolve())]
    if case['kind'] == 'negative':
        command += ['--overwrite'] if entrypoint == 'cli' else ['--probe-existing-output']
    if entrypoint == 'api':
        for key, value in sorted(case.get('host_limits', {}).items()):
            if key not in LIMITS or type(value) is not int or value <= 0:
                raise ValueError('Unsupported host quota')
            command += ['--limit', f'{key}={value}']
        if 'invalid_scalar' in case:
            scalar = case['invalid_scalar']
            if type(scalar) is not int or not 0xD800 <= scalar <= 0xDFFF:
                raise ValueError('Only an isolated surrogate probe is allowed')
            command += ['--invalid-scalar', str(scalar)]
        if case.get('_pre_cancelled'): command += ['--pre-cancelled']
    elif case.get('host_limits') or 'invalid_scalar' in case:
        raise ValueError('Host flags must not be passed to production CLI')
    if fonts: command += ['--subset-fonts']
    for font in fonts: command += ['--font', str(font.resolve())]
    return command


def run_case(case: dict, entrypoint: str, executable: Path, font_paths: list[Path],
             out_dir: Path, timeout_s: float) -> dict:
    if entrypoint not in case['entrypoints']:
        raise ValueError('Inapplicable entrypoint')
    source = inside(Path(case['_root']), case['input_path'])
    if digest(source.read_bytes()) != case['input_sha256']:
        raise ValueError('Input fingerprint changed')
    work = out_dir / case['id'] / entrypoint
    work.mkdir(parents=True, exist_ok=False)
    pdf = work / 'result.pdf'
    if case['kind'] == 'negative': pdf.write_bytes(b'keep')
    proc = run_process(command_for(case, entrypoint, executable, font_paths, pdf), work/'process', timeout_s)
    row = {k: v for k,v in proc.items() if k not in ('stdout_path', 'stderr_path')}
    row.update(case_id=case['id'], entrypoint=entrypoint, source_sha=case.get('_source_sha'),
               engine_baseline_sha=BASELINE_SHA, environment_id=case.get('_environment_id'),
               build_mode=case.get('_build_mode', 'unverified'), input_sha256=case['input_sha256'],
               font_inputs=[{'name': f.name, 'sha256': digest(f.read_bytes())} for f in font_paths],
               host_limits=case.get('host_limits', {}), status='infrastructure-error',
               pdf_path=None, pdf_sha256=None, pdf_bytes=None, error_code=None)
    if proc['error'] or proc['timed_out'] or not proc['cleanup_ok']:
        write_json(work/'result.json', row); return row
    raw = Path(proc['stdout_path']).read_bytes()
    err = Path(proc['stderr_path']).read_text(encoding='utf-8', errors='replace')
    row['stderr'] = err[:12000]
    host = None
    if entrypoint == 'api':
        try: host = json.loads(raw)
        except (ValueError, UnicodeError):
            row['error'] = 'API host did not emit JSON'; write_json(work/'result.json', row); return row
        if not isinstance(host, dict):
            row['error'] = 'API host emitted wrong record shape'; write_json(work/'result.json', row); return row
        row['host'] = host; row['error_code'] = host.get('code')
    else:
        found = re.search(r'\b(FPDF[0-9]{4})\b', err)
        row['error_code'] = found.group(1) if found else None
        if proc['returncode'] == 0 and case.get('_stdout'): pdf.write_bytes(raw)
    expectation = case['entrypoints'][entrypoint]
    unchanged = pdf.is_file() and pdf.read_bytes() == b'keep'
    row['output_unchanged'] = unchanged if case['kind'] == 'negative' else None
    has_pdf = pdf.is_file() and pdf.read_bytes().startswith(b'%PDF-')
    if proc['returncode'] == 0 and case['kind'] == 'negative':
        # A successful conversion violates the negative contract even when
        # the deliberately prefilled caller stream begins with keep%PDF.
        row['status'] = 'unexpected-success'
        if pdf.is_file():
            row.update(pdf_path=str(pdf), pdf_sha256=digest(pdf.read_bytes()), pdf_bytes=pdf.stat().st_size)
    elif proc['returncode'] == 0 and has_pdf:
        row.update(pdf_path=str(pdf), pdf_sha256=digest(pdf.read_bytes()), pdf_bytes=pdf.stat().st_size)
        row['status'] = 'candidate'
    elif proc['returncode'] == 0:
        row['error'] = 'Success exit without a PDF'
    elif case['kind'] in {'negative', 'target'}:
        correct = (proc['returncode'] == expectation['expected_exit'] and row['error_code'] == expectation['expected_code'])
        if case['kind'] == 'negative':
            correct = correct and unchanged and (host is None or host.get('output_unchanged') is True)
            row['status'] = 'expected-error' if correct else 'wrong-error'
        else:
            row['status'] = 'unsupported' if correct and not pdf.exists() else 'infrastructure-error'
    else: row['status'] = 'regression'
    write_json(work/'result.json', row)
    return row


def capture_environment(executables: dict[str, Path], font_paths: list[Path]) -> dict:
    from acceptance_build import validate_build_provenance
    build = validate_build_provenance(executables, os.environ.get('FACTSPDF_SOURCE_SHA'))
    def version(args):
        try:
            p = subprocess.run(args, capture_output=True, timeout=15)
            if p.returncode != 0: raise ValueError('version command failed')
            return (p.stdout + p.stderr).decode('utf-8', errors='replace').strip().splitlines()[0]
        except (OSError, ValueError, subprocess.SubprocessError): return None
    tools = {name: version([name, flag]) for name,flag in
             [('qpdf','--version'),('pdfinfo','-v'),('pdftotext','-v'),('pdftoppm','-v'),('chromium','--version')]}
    chrome = shutil.which('google-chrome') or shutil.which('chromium') or shutil.which('chromium-browser')
    tools['chromium'] = version([chrome,'--version']) if chrome else None
    runtime = version(['dotnet', '--version'])
    fonts = [{'name': f.name, 'sha256': digest(f.read_bytes()), 'bytes': f.stat().st_size} for f in font_paths]
    compatibility = {'system': platform.system(), 'architecture': platform.machine(), 'tools': tools,
                     'fonts': fonts, 'sdk': runtime, 'raster_dpi': 120}
    cpu = None; memory = None
    if Path('/proc/cpuinfo').is_file():
        m = re.search(r'^model name\s*:\s*(.+)$', Path('/proc/cpuinfo').read_text(), re.M)
        cpu = m.group(1) if m else None
    if Path('/proc/meminfo').is_file():
        m = re.search(r'^MemTotal:\s+(\d+) kB$', Path('/proc/meminfo').read_text(), re.M)
        memory = int(m.group(1))*1024 if m else None
    return {**compatibility, 'environment_id': digest(json.dumps(compatibility, sort_keys=True).encode()),
            'machine': {'cpu': cpu, 'memory_bytes': memory, 'platform': platform.platform(),
                        'shared_ci': os.environ.get('GITHUB_ACTIONS') == 'true'},
            'executables': {name: {'name': path.name, 'sha256': digest(path.read_bytes())} for name,path in executables.items()},
            'font_source': 'Explicit caller inputs; CI uses fonts-dejavu-core and fonts-droid-fallback. See docs/acceptance/README.md for notices.',
            'source_sha': os.environ.get('FACTSPDF_SOURCE_SHA'), 'engine_baseline_sha': BASELINE_SHA,
            'build_provenance': build}


if __name__ == '__main__':
    from acceptance_pipeline import main
    main()
