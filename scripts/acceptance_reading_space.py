"""Two real-PDF visual regressions; candidate spacing is not human approval.

Run after capture: python3 scripts/acceptance_reading_space.py PUBLIC OUTPUT.json
Uses the same Poppler/Pillow inspectors as the acceptance pipeline.
"""
from pathlib import Path
import argparse
import json
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from PIL import Image
from acceptance_corpus import digest, write_json
from css_box_visual_oracle import validate_fragment_edges


def measure(pdf: Path, directory: Path):
    directory.mkdir(parents=True)
    xml = subprocess.run(['pdftotext', '-bbox-layout', str(pdf), '-'],
                         check=True, capture_output=True, timeout=30).stdout
    pages = []
    for page in ET.fromstring(xml).iter():
        if page.tag.rsplit('}', 1)[-1] != 'page':
            continue
        pages.append([{'text': ''.join(w.itertext()),
                       'top': float(w.attrib['yMin'])}
                      for w in page.iter() if w.tag.rsplit('}', 1)[-1] == 'word'])
    subprocess.run(['pdftoppm', '-r', '120', '-png', str(pdf), str(directory/'page')],
                   check=True, capture_output=True, timeout=120)
    images = sorted(directory.glob('page-*.png'), key=lambda p: int(p.stem.split('-')[-1]))
    if len(images) != len(pages):
        raise ValueError('Missing raster pages')
    return pages, images


def check(public: Path, output: Path, renderers=('native', 'chrome')):
    report = {'source_sha': json.loads((public/'environment.json').read_text())['source_sha'],
              'review_status': 'pending-review', 'measurements': [], 'failures': []}
    with tempfile.TemporaryDirectory() as temp:
        for renderer in renderers:
            for ident in ('B01', 'B03'):
                pdf = public/ident/(renderer+'.pdf')
                pages, images = measure(pdf, Path(temp)/renderer/ident)
                row = {'case': ident, 'renderer': renderer, 'pdf_sha256': digest(pdf.read_bytes())}
                if ident == 'B01':
                    if len(pages) != 1:
                        raise ValueError('B01 must remain one page')
                    words = pages[0]
                    start = next(i for i, w in enumerate(words) if w['text'] == 'Final')
                    end = next(i for i in range(start+1, len(words)) if words[i]['text'] == 'No')
                    title_top = min(w['top'] for w in words[start:end])
                    with Image.open(images[0]) as image:
                        rgb = image.convert('RGB')
                        # A full-width panel border, never text or thin side edges.
                        rows = [y for y in range(rgb.height)
                                if sum(all(abs(c-t) <= 6 for c, t in zip(rgb.getpixel((x,y)), (29,69,104)))
                                       for x in range(rgb.width)) >= 700]
                    if not rows:
                        raise ValueError('B01 panel border is missing')
                    border_bottom = (max(rows)+1)*72/120
                    gap = title_top-border_bottom
                    row.update(title_bbox_top_pt=title_top, raster_border_bottom_pt=border_bottom,
                               gap_pt=gap, minimum_gap_pt=6, passed=gap >= 6)
                else:
                    if len(pages) != 3:
                        raise ValueError('B03 must retain three slice fragments')
                    row['fragment_edges'] = validate_fragment_edges(images, '#1d4568')
                    tops = [min(w['top'] for w in p) for p in pages]
                    gaps = [y-36 for y in tops[1:]]
                    row.update(first_word_bbox_top_pt=tops, continuation_gap_pt=gaps,
                               minimum_gap_pt=8, passed=all(g >= 8 for g in gaps))
                report['measurements'].append(row)
                if not row['passed']:
                    report['failures'].append(renderer+' '+ident+' reading space')
    report['passed'] = not report['failures']
    write_json(output, report)
    if report['failures']:
        raise AssertionError('; '.join(report['failures']))
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('public', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--renderer', choices=('native', 'chrome'), action='append',
                        help='Inspect only this renderer; default requires both')
    args = parser.parse_args()
    print(json.dumps(check(args.public, args.output, args.renderer or ('native', 'chrome')), indent=2))
