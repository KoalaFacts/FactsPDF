"""Validate the Tagged PDF the CLI writes for every example with veraPDF's PDF/UA-1 profile.

    python3 scripts/verify_tagged_pdf.py <cli> --verapdf <verapdf launcher> --font <regular.ttf> [--font <fallback.ttf> ...]
                                                [--images <png_fixtures output>] [--output <folder>]

Every example is rendered with the supplied fonts for all four roles (an example without an html lang
attribute gets lang="en" in a copy, as PDF/UA needs a document language). The invariant: a PDF that identifies
itself as PDF/UA-1 (pdfuaid:part in its XMP) passes the profile with no failed rule, and a named set of
examples must claim it. A PDF that does not claim it is not judged (a skipped heading level or an image without
alt text cannot be repaired by the engine). The untagged switch is checked to write no structure.
"""
import argparse
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

# Examples written to be accessible: they must claim PDF/UA-1 and pass.
MUST_CLAIM = ['quick-start', 'first-document', 'lists-report', 'business-table-rich-cells', 'image-list-composition',
              'branded-business-report', 'png-brand-report']


def render(cli, source, destination, fonts, images, extra=()):
    command = [cli, str(source), str(destination), '--overwrite', '--subset-fonts', '--title', 'Verification title']
    for role in ('--font', '--font-bold', '--font-italic', '--font-bold-italic'):
        for font in fonts:
            command += [role, font]
    if images:
        command += ['--image', f'logo={images}/brand-mark.png', '--image', f'chart={images}/rgb.png',
                    '--image', f'quality-chart={images}/rgba.png']
    return subprocess.run([*command, *extra], capture_output=True, text=True)


def verapdf(launcher, pdfs):
    results = {}
    # In batches, so the command line stays short on every platform. veraPDF exits 1 when any file is not
    # compliant: the report, not the exit code, says which.
    for start in range(0, len(pdfs), 20):
        run = subprocess.run([launcher, '--flavour', 'ua1', '--format', 'json', *map(str, pdfs[start:start + 20])],
                             capture_output=True, text=True)
        for job in json.loads(run.stdout)['report']['jobs']:
            result = job['validationResult'][0]
            failures = [(rule['clause'], rule['testNumber'], rule['failedChecks'], rule['description'])
                        for rule in result['details']['ruleSummaries']]
            results[Path(job['itemDetails']['name']).name] = (result['compliant'], failures)
    return results


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('cli')
    parser.add_argument('--verapdf', required=True)
    parser.add_argument('--font', action='append', required=True)
    parser.add_argument('--images')
    parser.add_argument('--output')
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    work = Path(arguments.output) if arguments.output else Path(tempfile.mkdtemp(prefix='factspdf-tagged-'))
    (work / 'html').mkdir(parents=True, exist_ok=True)
    (work / 'pdf').mkdir(parents=True, exist_ok=True)
    rendered, skipped = [], []
    for example in sorted((root / 'examples').glob('*.html')):
        html = example.read_text(encoding='utf-8')
        if 'lang=' not in html.lower():
            html = re.sub('<html', '<html lang="en"', html, count=1, flags=re.IGNORECASE) \
                if re.search('<html', html, re.IGNORECASE) else f'<html lang="en">{html}</html>'
        source = work / 'html' / example.name
        source.write_text(html, encoding='utf-8')
        destination = work / 'pdf' / (example.stem + '.pdf')
        result = render(arguments.cli, source, destination, arguments.font, arguments.images)
        if result.returncode == 0:
            rendered.append(destination)
        else:
            skipped.append(example.stem)
    results = verapdf(arguments.verapdf, rendered)
    failed, claimed = [], []
    for pdf in rendered:
        claims = b'pdfuaid:part' in pdf.read_bytes()
        compliant, failures = results[pdf.name]
        if claims:
            claimed.append(pdf.stem)
        if claims and not compliant:
            failed.append((pdf.stem, failures))
    for name in MUST_CLAIM:
        if name not in claimed:
            failed.append((name, 'does not claim PDF/UA-1'))
    untagged = work / 'untagged.pdf'
    check = render(arguments.cli, work / 'html' / 'quick-start.html', untagged, arguments.font, arguments.images, ['--untagged'])
    if check.returncode != 0 or b'StructTreeRoot' in untagged.read_bytes() or b'MarkInfo' in untagged.read_bytes():
        failed.append(('--untagged', 'writes structure or fails'))
    print(f'{len(rendered)} examples rendered ({len(skipped)} need other resources), {len(claimed)} claim PDF/UA-1, '
          f'{len(failed)} problems')
    for name, why in failed:
        print(f'  {name}: {why}')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
