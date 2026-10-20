"""Run the published Windows x64 native CLI itself and inspect its real PDF output."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import struct
import subprocess
import sys
import tempfile


def digest(data): return hashlib.sha256(data).hexdigest()


def validate_platform(system, machine):
    assert system == 'Windows' and machine.lower() in ('amd64', 'x86_64'), 'Requires actual Windows x64 execution'


def validate_pe(data):
    assert len(data) >= 64 and data[:2] == b'MZ', 'Missing PE DOS header'
    offset = struct.unpack_from('<I', data, 0x3c)[0]
    assert offset >= 64 and offset + 24 <= len(data), 'Truncated PE header'
    assert data[offset:offset+4] == b'PE\0\0', 'Missing PE signature'
    assert struct.unpack_from('<H', data, offset+4)[0] == 0x8664, 'Expected AMD64 PE machine'
    size, flags = struct.unpack_from('<HH', data, offset+20)
    assert flags & 2 and not flags & 0x2000, 'Expected executable, not DLL'
    optional = offset + 24
    assert size >= 240 and optional + size <= len(data), 'Truncated PE optional header'
    assert struct.unpack_from('<H', data, optional)[0] == 0x20b, 'Expected PE32+'
    count = struct.unpack_from('<I', data, optional+108)[0]
    assert 15 <= count <= (size-112)//8, 'Missing PE data directories'
    assert struct.unpack_from('<II', data, optional+112+14*8) == (0, 0), 'Unexpected managed CLR header'
    # An ordinary managed apphost also has this PE shape. AOT provenance additionally
    # requires the actual PublishAot build and successful isolated direct execution.
    return dict(machine='AMD64', format='PE32+', managedHeaderAbsent=True)


def stage_executable(source, directory):
    target = directory / 'FactsPDF.Cli.exe'
    shutil.copyfile(source, target)
    assert sorted(p.name for p in directory.iterdir()) == [target.name], 'Managed sidecars in execution directory'
    return target


def validate_diagnostic(result):
    expected = "FPDF1102: Element 'script' is not supported in this development slice.\n"
    assert result.returncode == 3 and not result.stdout, 'Unsupported input exit/stdout'
    assert result.stderr.decode('utf-8').replace('\r\n', '\n') == expected, 'Exact unsupported input diagnostic'


def validate_success(result, pdf_bytes):
    assert result.returncode == 0 and not result.stdout, 'Successful CLI exit/PDF stdout'
    expected = f'Wrote 2 page(s), {pdf_bytes} bytes.\n'
    assert result.stderr.decode('utf-8').replace('\r\n', '\n') == expected, 'Existing CLI success status'


def validate_destination(path, original):
    assert path.read_bytes() == original if original is not None else not path.exists(), 'Destination preservation'
    assert not list(path.parent.glob('.factspdf-*.tmp')), 'Temporary output leak'


def reverse_page_references(data):
    pattern = rb'(/Kids\s*\[\s*)(\d+\s+0\s+R)(\s+)(\d+\s+0\s+R)(\s*\])'
    assert len(re.findall(pattern, data)) == 1, 'Expected one two-page Kids array'
    changed = re.sub(pattern, lambda m: m[1]+m[4]+m[3]+m[2]+m[5], data)
    assert len(changed) == len(data) and changed != data, 'Page mutation must preserve object offsets'
    return changed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--executable', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    validate_platform(platform.system(), platform.machine())
    root = Path(__file__).resolve().parents[1]
    source = args.executable.resolve(); output = args.output.resolve()
    assert source.name == 'FactsPDF.Cli.exe', 'Expected the published CLI executable'
    pe = validate_pe(source.read_bytes())
    output.mkdir(parents=True, exist_ok=False)
    commands = []; versions = {}
    with tempfile.TemporaryDirectory(prefix='factspdf-windows-aot-') as temporary:
        work = Path(temporary); executable = stage_executable(source, work)
        assert executable.read_bytes() == source.read_bytes(), 'Staged executable bytes'

        def execute(*command, expected_exit=0):
            result = subprocess.run([str(c) for c in command], cwd=work, capture_output=True, timeout=90)
            stem = output / f'command-{len(commands)+1:02}'
            stem.with_suffix('.stdout').write_bytes(result.stdout)
            stem.with_suffix('.stderr').write_bytes(result.stderr)
            commands.append(dict(command=list(map(str, command)), cwd=str(work), exitCode=result.returncode,
                                 stdout=stem.with_suffix('.stdout').name, stderr=stem.with_suffix('.stderr').name))
            assert result.returncode == expected_exit, ('Unexpected process exit', command, result.returncode, result.stderr)
            return result

        for name in ('pdfinfo', 'pdftotext', 'pdftoppm'):
            path = shutil.which(name); assert path, 'Missing independent tool: '+name
            result = execute(path, '-v')
            versions[name] = dict(path=path, version=(result.stdout+result.stderr).decode('utf-8').strip())
        html = root / 'examples/first-document.html'
        bad = root / 'examples/first-use/unsupported.html'
        pdf = output / 'native output.pdf'
        result = execute(executable, html, pdf)
        validate_success(result, pdf.stat().st_size)
        execute(sys.executable, root / 'scripts/verify_sample.py', pdf)
        images = [output / f'native output-{page}.png' for page in (1, 2)]
        assert all(p.is_file() for p in images), 'Missing page previews'
        repeated = output / 'repeat.pdf'
        validate_success(execute(executable, html, repeated), repeated.stat().st_size)
        assert repeated.read_bytes() == pdf.read_bytes(), 'Repeat determinism'
        negatives = []
        for name in ('existing', 'absent', 'stdout'):
            target = output / (name+' target.pdf'); original = b'original Windows target bytes' if name == 'existing' else None
            if original is not None: target.write_bytes(original)
            result = execute(executable, bad, '-' if name == 'stdout' else target, '--overwrite', expected_exit=3)
            validate_diagnostic(result); validate_destination(target, original)
            negatives.append(dict(target=name, exitCode=3, diagnostic='FPDF1102', exactMessage=True, outputPreserved=True))
        recovered = output / 'existing target.pdf'
        validate_success(execute(executable, html, recovered, '--overwrite'), recovered.stat().st_size)
        assert recovered.read_bytes() == pdf.read_bytes(), 'Recovery after errors'
        # This is a valid reordered real PDF: reference lengths and xref offsets are unchanged.
        mutant = output / 'reordered.pdf'; mutant.write_bytes(reverse_page_references(pdf.read_bytes()))
        rejected = execute(sys.executable, root / 'scripts/verify_sample.py', mutant, expected_exit=1)
        assert b'Page break was not honored' in rejected.stderr, 'Inspector must reject actual page order'
        mutant.unlink()
        report = dict(sourceHead=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
            dirty=bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=root).strip()),
            platform=platform.platform(), machine=platform.machine(), runnerImage=os.environ.get('ImageVersion'),
            entry='direct isolated FactsPDF.Cli.exe', pe=pe, executableSha256=digest(source.read_bytes()),
            executableBytes=source.stat().st_size, isolatedDirectory=str(work), onlyExecutableStaged=True,
            inputSha256=digest(html.read_bytes()), unsupportedInputSha256=digest(bad.read_bytes()),
            pdfSha256=digest(pdf.read_bytes()), pdfBytes=pdf.stat().st_size, pages=2,
            pageImages=[dict(file=p.name, sha256=digest(p.read_bytes())) for p in images],
            tools=versions, commands=commands, negativeCases=negatives, repeatBytesEqual=True,
            recoveryBytesEqual=True, reorderedPdfRejected=True, published=False,
            visualReferenceApproval='not requested')
        (output / 'report.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    summary = dict(sourceHead=report['sourceHead'], platform=report['platform'], machine=report['machine'],
        executableSha256=report['executableSha256'], pdfSha256=report['pdfSha256'], pdfBytes=report['pdfBytes'],
        pages=2, directIsolatedExe=True, exactDiagnosticAndPreservationCases=3, recovery=True,
        reorderedPdfRejected=True, isolatedDirectoryRemoved=not work.exists())
    print(json.dumps(summary))
    if os.environ.get('GITHUB_ACTIONS') == 'true':
        # Result metadata only, never PDF bytes or an alternate artifact-download path.
        print('::notice title=Windows x64 Native AOT acceptance::'+json.dumps(summary).replace('%', '%25'))
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as stream:
            stream.write('Windows x64 Native AOT acceptance passed.\n\n```json\n'+json.dumps(summary, indent=2)+'\n```\n')


if __name__ == '__main__': main()
