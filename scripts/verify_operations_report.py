"""Inspect the separate reading sample; never approve technical references."""
from __future__ import annotations
import argparse
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import subprocess
from acceptance_inspect import check_boxes
from css_visual_differential import BROWSER_NORMALIZATION, VisibleText


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compact(text):
    return re.sub(r'\s+', '', text)


def inspect(pdf, expected, output, *, browser=False):
    output.mkdir(parents=True, exist_ok=False)
    qpdf = subprocess.run(['qpdf', '--check', str(pdf)], capture_output=True, check=True)
    (output/'qpdf.txt').write_bytes(qpdf.stdout + qpdf.stderr)
    text = subprocess.check_output(['pdftotext', '-raw', str(pdf), '-']).decode('utf-8')
    (output/'text.txt').write_text(text)
    if compact(text) != compact(expected):
        raise ValueError('Full visible text differs (only layout whitespace is ignored)')
    # Metadata ranges repeat four IDs. Validate all occurrences, not a set.
    if re.findall(r'R\d{4}', text) != re.findall(r'R\d{4}', expected):
        raise ValueError('Record IDs are missing, duplicated or reordered')
    xml = subprocess.check_output(['pdftotext', '-bbox-layout', str(pdf), '-'])
    (output/'bounds.xml').write_bytes(xml)
    pages = check_boxes(xml, 36, 0.5 if browser else 0.01)
    subprocess.run(['pdftoppm', '-r', '120', '-png', str(pdf), str(output/'page')], check=True)
    images = sorted(output.glob('page-*.png'))
    if len(images) != len(pages):
        raise ValueError('Missing page raster')
    rows = []
    for i, page in enumerate(pages):
        words = page['words']
        if not words:
            raise ValueError('Empty page')
        rows.append({'page': i+1, 'first_words': ' '.join(w['text'] for w in words[:8]),
                     'top': min(w['y0'] for w in words), 'bottom': max(w['y1'] for w in words),
                     'png_sha256': digest(images[i])})
    return {'pdf_sha256': digest(pdf), 'pages': rows, 'full_text': True,
            'record_order': True, 'bounds_36pt': True, 'qpdf': True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--html', type=Path, default=Path('examples/operations-report.html'))
    parser.add_argument('--native', type=Path, required=True)
    parser.add_argument('--api', type=Path, required=True)
    parser.add_argument('--chrome', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    html = args.html.read_text()
    visible = VisibleText(); visible.feed(html)
    expected = ''.join(visible.parts)
    if digest(args.native) != digest(args.api):
        raise ValueError('CLI/API output differs')
    wrapper = html.replace('</head>', BROWSER_NORMALIZATION +
        '<style>*{-webkit-print-color-adjust:exact;print-color-adjust:exact}</style></head>')
    (args.output/'chrome-input.html').write_text(wrapper)
    report = {'input_sha256': digest(args.html), 'cli_api_identical': True,
              'native': inspect(args.native, expected, args.output/'native'),
              'chrome': None, 'human_reference_approved': False,
              'scope': 'reading sample only; original technical fixtures and gates unchanged'}
    if args.chrome:
        report['chrome'] = inspect(args.chrome, expected, args.output/'chrome', browser=True)
    (args.output/'inspection.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
