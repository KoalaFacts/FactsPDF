"""Capture one reading sample with the existing trusted acceptance tools."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
from acceptance_corpus import digest, write_json
from acceptance_run import capture_environment, run_case
from acceptance_inspect import chrome_reference
from acceptance_pipeline import check_entrypoints
from css_visual_differential import VisibleText
from verify_operations_report import inspect


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cli', type=Path, required=True)
    parser.add_argument('--host', type=Path, required=True)
    parser.add_argument('--font', type=Path, action='append', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    public = args.output/'public'; public.mkdir()
    source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    write_json(public/'summary.json', {'source_sha': source, 'status': 'incomplete',
                                     'human_reference_approved': False})
    environment = capture_environment({'cli': args.cli, 'api': args.host}, args.font)
    runtime = json.loads(subprocess.check_output([str(args.host.resolve()), 'info'], timeout=30))
    if (environment['source_sha'] != source or runtime.get('source_sha') != source or
            runtime.get('dynamic_code_supported') is not False):
        raise ValueError('Exact-source Native AOT host is required')
    environment['host_runtime'] = runtime
    write_json(public/'environment.json', environment)
    html = Path('examples/operations-report.html').resolve()
    visible = VisibleText(); visible.feed(html.read_text(encoding='utf-8'))
    expected = ''.join(visible.parts)
    case = {'id': 'operations-report', 'kind': 'baseline', '_root': html.parent,
            'input_path': html.name, 'input_sha256': digest(html.read_bytes()),
            'checks': {'text_blocks': [expected]}, 'records': re.findall(r'R\d{4}', expected),
            'entrypoints': {entry: {'expected_exit': 0, 'expected_code': None} for entry in ('cli', 'api')},
            '_source_sha': source, '_environment_id': environment['environment_id'],
            '_build_mode': environment['build_provenance']['build_mode']}
    rows = [run_case(case, entry, executable, args.font, args.output/'runs', 120)
            for entry, executable in [('cli', args.cli), ('api', args.host)]]
    write_json(public/'runs.json', rows)
    check_entrypoints(case, rows)
    shutil.copyfile(rows[0]['pdf_path'], public/'native.pdf')
    shutil.copyfile(html, public/'operations-report.html')
    native = inspect(public/'native.pdf', expected, public/'native')
    chrome = chrome_reference(html, case, args.font, environment, args.output/'browser')
    shutil.copyfile(args.output/'browser/chrome.pdf', public/'chrome.pdf')
    shutil.copyfile(args.output/'browser/chrome-input.html', public/'chrome-input.html')
    # Require the same strict reading-sample bounds; a browser discrepancy is
    # not silently converted into a successful comparison.
    browser = inspect(public/'chrome.pdf', expected, public/'chrome', browser=True)
    write_json(public/'inspection.json', {'source_sha': source,
        'input_sha256': case['input_sha256'], 'cli_api_identical': True,
        'native': native, 'chrome': browser, 'chrome_capture': chrome,
        'human_reference_approved': False})
    write_json(public/'summary.json', {'source_sha': source, 'status': 'checked-candidate',
        'native_pages': len(native['pages']), 'chrome_pages': len(browser['pages']),
        'page_counts_equal': len(native['pages']) == len(browser['pages']),
        'human_reference_approved': False,
        'scope': 'Technical inspection only; inspect every raster before reading approval'})


if __name__ == '__main__':
    main()
