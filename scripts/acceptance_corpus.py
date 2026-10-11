"""Deterministic acceptance inputs; never equate expected rejection with support."""
from __future__ import annotations
import argparse
import hashlib
import html
import json
from pathlib import Path, PurePosixPath
import re

BASELINE_SHA = '1e3f24f21b2e9bbdc5db6e05066998c07a6f9af6'
STATES = {
    'baseline': {'passed', 'regression', 'pending-review', 'infrastructure-error'},
    'negative': {'expected-error', 'unexpected-success', 'wrong-error', 'infrastructure-error'},
    'target': {'unsupported', 'blocked-design', 'candidate', 'verified', 'infrastructure-error'},
}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n', encoding='utf-8')


def inside(root: Path, relative: str) -> Path:
    if not isinstance(relative, str) or not relative or '\\' in relative or ':' in relative:
        raise ValueError('Expected a portable relative path')
    part = PurePosixPath(relative)
    if part.is_absolute() or '..' in part.parts or '.' == relative:
        raise ValueError('Path escapes the corpus')
    resolved = (root / relative).resolve()
    if not resolved.is_relative_to(root.resolve()):
        raise ValueError('Symlink escapes the corpus')
    return resolved


def load_manifest(path: Path) -> dict:
    m = json.loads(path.read_text(encoding='utf-8'))
    if m.get('schema_version') != 1 or m.get('engine_baseline_sha') != BASELINE_SHA:
        raise ValueError('Unknown schema or acceptance baseline (legacy M8 is separate)')
    if not isinstance(m.get('cases'), list) or not m['cases']:
        raise ValueError('Empty case manifest')
    seen = set()
    for case in m['cases']:
        ident = case.get('id')
        if not isinstance(ident, str) or not re.fullmatch(r'[A-Za-z0-9-]+', ident) or ident in seen:
            raise ValueError('Invalid or duplicate case ID')
        seen.add(ident)
        if case.get('kind') not in STATES or not case.get('requirements') or not isinstance(case.get('checks'), dict):
            raise ValueError('Missing case contract')
        if not isinstance(case.get('records'), list) or not isinstance(case.get('font_roles'), list):
            raise ValueError('Missing records/font roles')
        if len(set(case['records'])) != len(case['records']):
            raise ValueError('Duplicate record ID')
        entries = case.get('entrypoints')
        if not isinstance(entries, dict) or set(entries) - {'cli', 'api'}:
            raise ValueError('Explicit entrypoint contracts required')
        for contract in entries.values():
            if not isinstance(contract, dict) or type(contract.get('expected_exit')) is not int or contract['expected_exit'] not in (0, 3):
                raise ValueError('Invalid expected exit')
            code = contract.get('expected_code')
            if code is not None and not re.fullmatch(r'FPDF[0-9]{4}', code):
                raise ValueError('Invalid expected diagnostic')
        if case['kind'] != 'target' and not entries:
            raise ValueError('Runnable case must declare applicable entries')
        if case['kind'] == 'baseline' and set(entries) != {'cli', 'api'}:
            raise ValueError('Baseline requires CLI/API parity')
        limits = case.get('host_limits', {})
        if set(limits) - {'MaxPages', 'MaxInputCharacters', 'MaxElements', 'MaxDepth', 'MaxCssCharacters', 'MaxCssDeclarations', 'MaxDisplayCommands', 'MaxOutputBytes'}:
            raise ValueError('Unknown host limit')
        if any(type(v) is not int or v <= 0 for v in limits.values()):
            raise ValueError('Positive integer limits required')
        if (limits or 'invalid_scalar' in case) and 'cli' in entries:
            raise ValueError('Host-only arguments cannot apply to production CLI')
        source = case.get('input_path')
        if source is None:
            if case['kind'] != 'target' or case.get('initial_status') != 'blocked-design':
                raise ValueError('Only a not-yet-designed target may omit input')
        else:
            file = inside(path.parent, source)
            if not file.is_file() or digest(file.read_bytes()) != case.get('input_sha256'):
                raise ValueError('Missing input or changed fingerprint: ' + ident)
        if case['kind'] == 'baseline' and not case['checks'].get('text_blocks'):
            raise ValueError('Baseline needs independently authored text expectations')
        if case['kind'] == 'negative' and not re.fullmatch(r'FPDF\d{4}', case.get('expected_code', '')):
            raise ValueError('Negative case needs an exact diagnostic code')
    return m


def summarize_results(manifest: dict, results: list[dict]) -> dict:
    cases = {c['id']: c for c in manifest['cases']}
    by_id = {}
    for row in results:
        ident = row.get('case_id')
        if ident not in cases or ident in by_id or row.get('status') not in STATES[cases[ident]['kind']]:
            raise ValueError('Unknown, duplicate or misclassified result')
        if row['status'] in {'verified', 'passed'}:
            required = set(cases[ident]['requirements'])
            if not required.issubset(row.get('verified_requirements', [])) or row.get('review_status') != 'approved':
                raise ValueError('Support needs every requirement and reviewed evidence')
            if not row.get('inspection_passed') or not row.get('evidence_sha256'):
                raise ValueError('Missing independently inspected output')
        by_id[ident] = row
    baseline = [c for c in cases.values() if c['kind'] == 'baseline']
    negative = [c for c in cases.values() if c['kind'] == 'negative']
    target = [c for c in cases.values() if c['kind'] == 'target']
    passed = sum(by_id.get(c['id'], {}).get('status') == 'passed' for c in baseline)
    return {
        'schema_version': 1, 'engine_baseline_sha': BASELINE_SHA,
        'baseline_regression_passed': passed, 'baseline_total': len(baseline),
        'negative_expected_errors': sum(by_id.get(c['id'], {}).get('status') == 'expected-error' for c in negative),
        'negative_total': len(negative),
        'target_verified_count': sum(by_id.get(c['id'], {}).get('status') == 'verified' for c in target),
        'target_total': len(target), 'evidence_complete': passed == len(baseline) and len(by_id) == len(cases)
            and all(by_id.get(c['id'], {}).get('status') == 'expected-error' for c in negative)
            and all(by_id.get(c['id'], {}).get('status') != 'infrastructure-error' for c in target),
        'preview_ready': False, 'missing_results': sorted(set(cases) - set(by_id)),
    }


def generate_corpus(root: Path) -> list[dict]:
    """Generate owned sample paths only; there is intentionally no approval operation."""
    root.mkdir(parents=True, exist_ok=True)
    records = [{'id': f'R{i:04}', 'text': f'R{i:04} - Item {i:03}: verified synthetic record. 合成记录已核对。'} for i in range(1, 101)]
    write_json(root / 'data/report-records.json', records)
    cases = []
    css = ('body{font-size:10pt;line-height:1.4;color:#1d4568}'
           'h1{font-size:20pt;margin-top:0pt;margin-bottom:12pt}h2{font-size:14pt;margin-top:0pt;margin-bottom:8pt}'
           'p{margin-top:0pt;margin-bottom:6pt}'
           '.panel{padding:10pt;border:1pt solid #1d4568;background-color:#e7f2fa}'
           '.next{break-before:page}')

    def add(ident, kind, blocks, requirement, content=None, records_ids=None, **extra):
        case = {'id': ident, 'kind': kind, 'requirements': requirement,
                'font_roles': ['latin', 'cjk'] if ident != 'B00' else [],
                'records': records_ids or [], 'checks': {'text_blocks': blocks} if kind == 'baseline' else {}, **extra}
        if content is None and kind == 'target':
            case.update(input_path=None, input_sha256=None, initial_status='blocked-design')
        else:
            if content is None:
                content = '<h1>' + html.escape(blocks[0]) + '</h1>' + ''.join('<p>' + html.escape(x) + '</p>' for x in blocks[1:])
            text = '<!DOCTYPE html><html><head><meta charset="utf-8"><style>' + css + '</style></head><body>' + content + '</body></html>\n'
            rel = f'{"baseline" if kind == "baseline" else "negative" if kind == "negative" else "targets"}/{ident}.html'
            file = inside(root, rel); file.parent.mkdir(parents=True, exist_ok=True)
            file.write_text(text, encoding='utf-8')
            case.update(input_path=rel, input_sha256=digest(file.read_bytes()))
            if kind == 'target': case['initial_status'] = 'unsupported'
        cases.append(case)

    add('B00', 'baseline', ['FactsPDF acceptance control', 'This report uses only documented text features.', 'No customer data or external resources.'], ['text-basic'])
    b = ['FactsPDF 双语简报', 'Bilingual report baseline', 'Summary 摘要',
         'This synthetic report checks readable output. 本报告只使用合成数据。',
         'Review panel 核对面板', 'Text and borders must remain visible. 文字与边框应当完整显示。',
         'Final note 结束说明', 'No missing text, no clipped content. 不缺字，不截断。']
    panel = '<section class="panel"><h2>' + b[4] + '</h2><p>' + b[5] + '</p></section>'
    add('B01', 'baseline', b, ['text-unicode', 'nested-panel'],
        '<h1>'+b[0]+'</h1><p>'+b[1]+'</p><h2>'+b[2]+'</h2><p>'+b[3]+'</p>'+panel+'<h2 style="padding-top:8pt">'+b[6]+'</h2><p>'+b[7]+'</p>')
    for count in (10, 30, 100):
        blocks = [f'Record report 记录报告 - {count}'] + [r['text'] for r in records[:count]] + ['End of records. 记录结束。']
        add(f'B02-{count}', 'baseline', blocks, ['record-order', 'text-unicode'], records_ids=[r['id'] for r in records[:count]])
    b = ['Continued panel 跨页面板', 'First section 第一部分', 'The container continues. 容器将延续到下一页。',
         'Second section 第二部分', 'No repeated or missing content. 内容不应重复或缺失。',
         'Third section 第三部分', 'The bottom border ends here. 下边框在这里结束。']
    add('B03', 'baseline', b, ['fragmentation', 'text-unicode'], '<section class="panel"><h1>'+b[0]+'</h1><h2>'+b[1]+'</h2><p>'+b[2]+'</p><h2 class="next" style="padding-top:10pt">'+b[3]+'</h2><p>'+b[4]+'</p><h2 class="next" style="padding-top:10pt">'+b[5]+'</h2><p>'+b[6]+'</p></section>')
    add('N-wide', 'negative', [], ['output-protection'], '<p>'+'X'*500+'</p>', expected_code='FPDF1302')
    add('N-pages', 'negative', [], ['page-budget'], '<p>One</p><p class="next">Two</p>', expected_code='FPDF1303', api_only=True, max_pages=1)
    for tag, scalar in [('high', 0xD800), ('low', 0xDC00)]:
        add('N-utf16-'+tag, 'negative', [], ['invalid-utf16'], '<p>runtime-invalid-input</p>', expected_code='FPDF1304', api_only=True, invalid_scalar=scalar)
    add('T-bold', 'target', [], ['real-bold-font'], '<p><strong>Real emphasis</strong></p>', expected_code='FPDF1102')
    add('T-list', 'target', [], ['list-numbering', 'list-wrap', 'list-nesting'], '<ol><li>First<ul><li>Nested</li></ul></li></ol>', expected_code='FPDF1102')
    for count in (10, 30, 100):
        add(f'T-table-{count}', 'target', [], ['fixed-columns', 'table-record-order', 'repeat-header'],
            '<table><tr><th>ID</th><th>Text</th></tr>'+''.join('<tr><td>'+r['id']+'</td><td>Synthetic row</td></tr>' for r in records[:count])+'</table>',
            records_ids=[r['id'] for r in records[:count]], expected_code='FPDF1102')
    for ident, req in [('T-jpeg', ['explicit-jpeg-resource']), ('T-page-furniture', ['page-number', 'header-footer']),
                       ('T-heading-keep', ['heading-with-paragraph']), ('T-complete-report', ['all-report-capabilities'])]:
        add(ident, 'target', [], req)
    add('N-zero', 'negative', [], ['zero-width'], "<div style='width:0'>X</div>", expected_code='FPDF1302')
    for ident, option, amount, code in [
        ('N-input', 'MaxInputCharacters', 10, 'FPDF1001'),
        ('N-elements', 'MaxElements', 2, 'FPDF1002'),
        ('N-depth', 'MaxDepth', 1, 'FPDF1003'),
        ('N-css-characters', 'MaxCssCharacters', 1, 'FPDF1205'),
        ('N-css-declarations', 'MaxCssDeclarations', 1, 'FPDF1205'),
        ('N-display', 'MaxDisplayCommands', 1, 'FPDF1401'),
        ('N-output', 'MaxOutputBytes', 128, 'FPDF1401')]:
        add(ident, 'negative', [], [option], '<p>A</p><p>B</p>',
            expected_code=code, api_only=True, host_limits={option: amount})
    for case in cases:
        if 'max_pages' in case:
            case['host_limits'] = {'MaxPages': case.pop('max_pages')}
        if case['input_path'] is None:
            case['entrypoints'] = {}
        else:
            entries = ['api'] if case.pop('api_only', False) else ['cli', 'api']
            case['entrypoints'] = {entry: {'expected_exit': 0 if case['kind'] == 'baseline' else 3,
                                         'expected_code': case.get('expected_code')} for entry in entries}
    m = {'schema_version': 1, 'engine_baseline_sha': BASELINE_SHA, 'page': {'width': 595.28, 'height': 841.89, 'margin': 36}, 'cases': cases}
    write_json(root / 'manifest.json', m)
    write_json(root / 'targets/requirements.json', {'schema_version': 1, 'status': 'not-implemented', 'targets': [c for c in cases if c['kind'] == 'target']})
    return cases


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('action', choices=['generate', 'validate']); p.add_argument('path', type=Path)
    args = p.parse_args()
    if args.action == 'generate': generate_corpus(args.path)
    else: load_manifest(args.path)
    print(args.action + ': OK')


if __name__ == '__main__': main()
