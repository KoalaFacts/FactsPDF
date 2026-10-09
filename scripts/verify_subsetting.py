"""Verify identical real-font inputs in full/subset modes; fonts are NEVER pre-subset.
All fontTools, qpdf and Poppler use is independent verification, not engine work.
Standalone font files are neither written here nor included in the artifact.
"""
import hashlib
from html.parser import HTMLParser
import io
import json
import os
from pathlib import Path
import platform
import re
import statistics
import struct
import subprocess
import sys
import time
from fontTools.ttLib import TTFont


def run(*args):
    result = subprocess.run([str(a) for a in args], capture_output=True, timeout=120)
    if result.returncode:
        raise RuntimeError(f'{args[0]} exited {result.returncode}: ' + result.stderr.decode('utf-8', errors='replace') + result.stdout.decode('utf-8', errors='replace'))
    return result.stdout


class Text(HTMLParser):
    def __init__(self):
        super().__init__(); self.parts = []; self.head = False
    def handle_starttag(self, tag, attrs):
        if tag == 'head': self.head = True
    def handle_endtag(self, tag):
        if tag == 'head': self.head = False
    def handle_data(self, text):
        if not self.head: self.parts.append(text)


def embedded_fonts(pdf):
    contents = pdf.read_bytes()
    ids = re.findall(rb'/FontFile2 (\d+) 0 R', contents)
    return [run('qpdf', pdf, '--show-object=' + value.decode('ascii'), '--filtered-stream-data') for value in ids]


def unicode_maps(pdf):
    ids = re.findall(rb'/ToUnicode (\d+) 0 R', pdf.read_bytes()); maps = []
    for value in ids:
        data = run('qpdf', pdf, '--show-object=' + value.decode('ascii'), '--filtered-stream-data')
        scalars = set()
        for block in re.findall(rb'beginbfchar\s+(.*?)\s+endbfchar', data, re.S):
            for pair in re.findall(rb'<[0-9A-F]+>\s+<([0-9A-F]+)>', block):
                scalars.add(ord(bytes.fromhex(pair.decode('ascii')).decode('utf-16-be')))
        maps.append(scalars)
    return maps


def sum32(data):
    data += b'\0' * (-len(data) % 4)
    return sum(struct.unpack('>' + 'I' * (len(data) // 4), data)) & 0xffffffff


def inspect_subsets(full, subset):
    originals = embedded_fonts(full); subsets = embedded_fonts(subset); maps = unicode_maps(full)
    assert len(originals) == len(subsets) == len(maps) == 2
    summary = []
    for old_data, new_data, scalars in zip(originals, subsets, maps):
        assert sum32(new_data) == 0xb1b0afba, 'Whole sfnt checksum'
        with TTFont(io.BytesIO(old_data), lazy=False) as original, TTFont(io.BytesIO(new_data), lazy=False) as changed:
            old_map = original.getBestCmap(); new_map = changed.getBestCmap()
            assert set(new_map) == scalars, 'Subset cmap contains wrong or unused characters'
            assert changed['OS/2'].fsType == original['OS/2'].fsType, 'Embedding permissions changed'
            closure = {'.notdef'}
            def include(name):
                if name in closure: return
                closure.add(name); glyph = original['glyf'][name]
                if glyph.isComposite():
                    for component in glyph.components: include(component.glyphName)
            for scalar in scalars: include(old_map[scalar])
            # Include a composite .notdef's dependencies too.
            if original['glyf']['.notdef'].isComposite():
                for c in original['glyf']['.notdef'].components: include(c.glyphName)
            assert changed['maxp'].numGlyphs == len(closure), 'Unexpected retained glyphs'
            assert changed['maxp'].numGlyphs < original['maxp'].numGlyphs
            for scalar in scalars:
                old, new = old_map[scalar], new_map[scalar]
                assert original['hmtx'][old] == changed['hmtx'][new], 'Advance/side-bearing changed'
                a = original['glyf'][old].getCoordinates(original['glyf'])
                b = changed['glyf'][new].getCoordinates(changed['glyf'])
                assert tuple(map(list, a)) == tuple(map(list, b)), 'Glyph outline changed'
            def legal_names(font):
                return {(n.platformID, n.platEncID, n.langID, n.nameID, n.string) for n in font['name'].names if n.nameID in {0, 7, 8, 9, 11, 13, 14}}
            assert legal_names(original) == legal_names(changed), 'Copyright/license metadata changed'
            for tag in ('fpgm', 'prep', 'cvt ', 'gasp'):
                if tag in original: assert original.getTableData(tag) == changed.getTableData(tag), 'Hint program/data changed'
            for tag, entry in changed.reader.tables.items():
                raw = new_data[entry.offset:entry.offset + entry.length]
                if tag == 'head': raw = raw[:8] + b'\0' * 4 + raw[12:]
                assert sum32(raw) == entry.checkSum, 'Table checksum: ' + tag
            summary.append({'original_glyphs': original['maxp'].numGlyphs, 'subset_glyphs': changed['maxp'].numGlyphs,
                            'used_scalars': len(scalars), 'original_font_bytes': len(old_data), 'subset_font_bytes': len(new_data)})
    return summary


def main():
    exe = Path(sys.argv[1]).resolve(); bench = Path(sys.argv[2]).resolve()
    fonts = [Path('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'), Path('/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf')]
    html = Path('examples/subset-document.html'); parsed = Text(); parsed.feed(html.read_text(encoding='utf-8'))
    expected = ''.join(parsed.parts); output = Path('artifacts/subsetting'); output.mkdir(parents=True, exist_ok=True)
    files = {mode: output / ('subset-document.pdf' if mode == 'subset' else 'full-document.pdf') for mode in ('full', 'subset')}
    def cli(mode):
        args = [exe, html, files[mode], '--overwrite']
        for font in fonts: args += ['--font', font]
        if mode == 'subset': args += ['--subset-fonts']
        return args
    text_by_mode = {}
    for mode, pdf in files.items():
        run(*cli(mode)); print(run('qpdf', '--check', pdf).decode())
        info = run('pdfinfo', pdf).decode(); assert re.search(r'^Pages:\s+2$', info, re.M), info
        listing = run('pdffonts', pdf).decode(); print(listing)
        rows = listing.splitlines()[2:]; pattern = r'yes\s+yes\s+yes' if mode == 'subset' else r'yes\s+no\s+yes'
        assert len(rows) == 2 and all('CID TrueType' in row and re.search(pattern, row) for row in rows)
        text_by_mode[mode] = run('pdftotext', '-raw', pdf, '-').decode()
        assert ''.join(text_by_mode[mode].split()) == ''.join(expected.split()), 'Text differs from HTML'
        run('pdftoppm', '-r', '120', pdf, output / mode)
    assert text_by_mode['full'] == text_by_mode['subset'], 'Text extraction differs between modes'
    for page in (1, 2):
        assert (output / f'full-{page}.ppm').read_bytes() == (output / f'subset-{page}.ppm').read_bytes(), 'Raster pixels changed'
    assert files['subset'].stat().st_size < files['full'].stat().st_size * 0.20, 'Real-font fixture did not shrink sufficiently'
    font_details = inspect_subsets(files['full'], files['subset'])
    run('pdftoppm', '-r', '120', '-png', files['subset'], output / 'subset-document')
    samples = {'full': [], 'subset': []}
    # Each sample starts a fresh process; alternate order. Filesystem/OS caches are NOT flushed.
    for trial in range(5):
        for mode in (('full', 'subset') if trial % 2 == 0 else ('subset', 'full')):
            rss = output / 'rss.txt'; start = time.perf_counter_ns()
            run('/usr/bin/time', '-f', '%M', '-o', rss, *cli(mode))
            samples[mode].append({'wall_ms': (time.perf_counter_ns() - start) / 1e6, 'peak_rss_kib': int(rss.read_text().strip())})
    managed = {}
    for mode in ('full', 'subset'):
        managed[mode] = json.loads(run(bench, mode, html, *fonts))
        assert managed[mode]['dynamic_code_supported'] is False, 'Benchmark is not actually AOT'
        assert managed[mode]['pages'] == 2 and managed[mode]['output_bytes'] == files[mode].stat().st_size
        assert managed[mode]['reused_font_conversion']['samples'] == 20
    modes = {}
    for mode in samples:
        modes[mode] = {'output_bytes': files[mode].stat().st_size,
                       'new_process_median_wall_ms': statistics.median(s['wall_ms'] for s in samples[mode]),
                       'new_process_median_peak_rss_kib': statistics.median(s['peak_rss_kib'] for s in samples[mode]),
                       'new_process_samples': samples[mode], 'aot_managed_baseline': managed[mode]}
    report = {'source_sha': os.environ.get('FACTSPDF_SOURCE_SHA'), 'checkout_sha': run('git', 'rev-parse', 'HEAD').decode().strip(),
              'os': platform.platform(), 'cpu': run('lscpu').decode(), 'dotnet': run('dotnet', '--info').decode(),
              'html_sha256': hashlib.sha256(html.read_bytes()).hexdigest(),
              'font_inputs': [{'name': f.name, 'bytes': f.stat().st_size, 'sha256': hashlib.sha256(f.read_bytes()).hexdigest()} for f in fonts],
              'native_cli_bytes': exe.stat().st_size, 'fonts': font_details, 'modes': modes,
              'subset_pdf_sha256': hashlib.sha256(files['subset'].read_bytes()).hexdigest(),
              'checks': {'text_equal': True, 'raster_pixels_equal_120dpi': True, 'glyph_metrics_outlines_equal': True},
              'caveats': ['One small two-page corpus on a shared CI runner, not guaranteed production performance.',
                          'Fresh-process CLI wall time includes time-wrapper, startup, font I/O/loading, rendering, output write/fsync. OS/filesystem caches are warm.',
                          'Per-process peak RSS includes all CLI phases, not just subsetting.',
                          'Managed allocation counts exclude native allocations; reused-font conversion excludes initial font load. Raw samples are retained.',
                          'Complete identical font inputs used in both modes. No fontTools preprocessing. FontTools only inspects outputs.']}
    path = output / 'measurements.json'; path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'modes': modes, 'fonts': font_details, 'checks': report['checks'], 'subset_pdf_sha256': report['subset_pdf_sha256']}, indent=2))
    # Public sample only. Never emit or upload standalone font files.


if __name__ == '__main__': main()
