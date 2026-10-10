"""Same-harness M8/M9 Native AOT evidence; timing is descriptive, never a gate."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import statistics
from pathlib import Path

BASELINE_SHA = '9b9ad5b04c3cc89a18f620b346589381b693695b'
EXPECTED_PAGES = {'plain': 1, 'nested': 1, 'fragmented': 6}


def workloads() -> dict[str, str]:
    plain = ''.join(f'<p>Plain document line {i:02d}.</p>' for i in range(24))
    opening = "<div style='padding:1pt;border:1pt solid #1d4568;background-color:#cee8fb'>"
    nested = opening * 12 + ''.join(f'<p>Nested document line {i:02d}.</p>' for i in range(8)) + '</div>' * 12
    fragments = []
    for page in range(6):
        before = 'break-before:page;' if page else ''
        fragments.append(f"<p style='{before}margin-bottom:0'>Fragment page {page + 1}.</p>")
        fragments.extend(f'<p>Page {page + 1} line {line:02d} with searchable text.</p>' for line in range(12))
    fragmented = "<section style='padding:4pt;border:2pt solid #1d4568;background-color:#cee8fb'>" + ''.join(fragments) + '</section>'
    return {'plain': plain, 'nested': nested, 'fragmented': fragmented}


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def _hash(value: object, length: int) -> bool:
    return isinstance(value, str) and re.fullmatch(f'[0-9a-f]{{{length}}}', value) is not None


def _number(value: object, *, integer: bool = False, positive: bool = False) -> bool:
    if isinstance(value, bool) or not isinstance(value, int if integer else (int, float)):
        return False
    try:
        return math.isfinite(value) and (value > 0 if positive else value >= 0)
    except OverflowError:
        return False


def validate_measurement(data: dict, name: str) -> None:
    """Reject malformed or misleading samples before comparing revisions."""
    try:
        _require(data['mode'] == 'full', 'Expected full/ASCII benchmark mode')
        _require(data['dynamic_code_supported'] is False, 'Expected actual Native AOT output')
        for key in ('runtime', 'os', 'architecture'):
            _require(isinstance(data[key], str) and bool(data[key].strip()), f'Missing {key}')
        _require(data['architecture'] == 'X64', 'Expected Linux x64 benchmark')
        for key in ('output_bytes', 'pages', 'process_lifetime_peak_working_set_bytes'):
            _require(_number(data[key], integer=True, positive=True), f'Invalid {key}')
        _require(data['pages'] == EXPECTED_PAGES[name], f'Wrong page count for {name}')
        expected_input = hashlib.sha256(workloads()[name].encode('utf-8')).hexdigest()
        _require(data['input_sha256'] == expected_input, f'Input mismatch for {name}')
        _require(_hash(data['output_sha256'], 64), 'Invalid PDF hash')
        sample = data['reused_font_conversion']
        _require(type(sample['samples']) is int and sample['samples'] == 20, 'Expected 20 samples')
        _require(isinstance(sample['raw'], list) and len(sample['raw']) == 20, 'Incomplete raw samples')
        for row in sample['raw']:
            _require(_number(row['ms']), 'Invalid sample milliseconds')
            _require(_number(row['managed_allocated_bytes'], integer=True), 'Invalid allocation sample')
        _require(_number(sample['median_ms']), 'Invalid median milliseconds')
        _require(_number(sample['median_managed_allocated_bytes'], integer=True), 'Invalid median allocation')
        _require(math.isclose(sample['median_ms'], statistics.median(row['ms'] for row in sample['raw']),
                              rel_tol=1e-9, abs_tol=1e-12), 'Median differs from raw timings')
        _require(sample['median_managed_allocated_bytes'] == int(statistics.median(
            row['managed_allocated_bytes'] for row in sample['raw'])), 'Median differs from raw allocations')
    except (KeyError, TypeError, IndexError) as error:
        raise ValueError(f'Malformed benchmark data for {name}: {error}') from error


def build_report(baseline: dict, candidate: dict, source_sha: str,
                 baseline_sha: str, harness_sha256: str) -> dict:
    _require(_hash(source_sha, 40) and source_sha != BASELINE_SHA, 'Invalid candidate revision')
    _require(baseline_sha == BASELINE_SHA, 'Unexpected baseline revision')
    _require(_hash(harness_sha256, 64), 'Invalid same-harness hash')
    _require(set(baseline) == set(EXPECTED_PAGES) == set(candidate), 'Missing or unexpected workload')
    cases = {}
    for name in EXPECTED_PAGES:
        a, b = baseline[name], candidate[name]
        validate_measurement(a, name)
        validate_measurement(b, name)
        for key in ('input_sha256', 'output_sha256', 'output_bytes', 'pages', 'runtime', 'os', 'architecture'):
            _require(a[key] == b[key], f'{name}: baseline/candidate {key} differ')
        elapsed = a['reused_font_conversion']['median_ms']
        cases[name] = {
            'baseline': a, 'candidate': b,
            'candidate_to_baseline_median_time_ratio': b['reused_font_conversion']['median_ms'] / elapsed if elapsed else None,
        }
    return {
        'baseline_sha': baseline_sha, 'source_sha': source_sha, 'harness_sha256': harness_sha256,
        'measurement_scope': 'Same harness and runner; fresh process per workload/revision. Twenty warm conversion samples; current-thread managed allocations exclude native allocations. Process-lifetime peak working set includes setup, warmups, hashing and reporting. No cold-start or statistical performance claim; no speed threshold.',
        'cases': cases, 'errors': [],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    generate = commands.add_parser('generate')
    generate.add_argument('directory', type=Path)
    compare = commands.add_parser('compare')
    compare.add_argument('baseline_directory', type=Path)
    compare.add_argument('candidate_directory', type=Path)
    compare.add_argument('source_sha')
    compare.add_argument('baseline_sha')
    compare.add_argument('harness_sha256')
    compare.add_argument('output', type=Path)
    args = parser.parse_args()
    if args.command == 'generate':
        args.directory.mkdir(parents=True, exist_ok=True)
        for name, html in workloads().items():
            (args.directory / f'{name}.html').write_bytes(html.encode('utf-8'))
        return 0
    a = {name: json.loads((args.baseline_directory / f'{name}.json').read_text()) for name in EXPECTED_PAGES}
    b = {name: json.loads((args.candidate_directory / f'{name}.json').read_text()) for name in EXPECTED_PAGES}
    report = build_report(a, b, args.source_sha, args.baseline_sha, args.harness_sha256)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    for name, case in report['cases'].items():
        print(f"{name}: parity verified, {case['candidate']['pages']} pages, ratio={case['candidate_to_baseline_median_time_ratio']}")
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
