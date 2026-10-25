"""Bounded, source-exact existing first-use examples; not a final candidate."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import zipfile

VERSION = '0.1.0-preview.1'
ARCHIVE = f'FactsPDF.{VERSION}.examples.zip'
FILES = {
    **{'examples/first-use/' + name: 'first-use/' + name for name in (
        'FirstUse.csproj', 'Program.cs', 'NuGet.Config', 'input.html',
        'unsupported.html', 'expected-pages.json')},
    'global.json': 'global.json',
    'LICENSE.md': 'LICENSE.md',
    'docs/private-examples-readme.md': 'README.md',
}
MAX_FILE = 1024 * 1024
MAX_TOTAL = 4 * 1024 * 1024


def assemble_examples(root: Path, output: Path, source_head: str) -> dict:
    root = root.resolve(strict=True)
    output = output.absolute()
    dest = output.resolve()
    if (not re.fullmatch('[0-9a-f]{40}', source_head) or output.exists() or
            output.is_symlink() or not output.parent.is_dir() or
            dest == root or root in dest.parents):
        raise ValueError('Exact source SHA and fresh external examples output required')

    def git(*args):
        return subprocess.check_output(['git', *args], cwd=root)

    if git('rev-parse', 'HEAD').decode().strip() != source_head:
        raise ValueError('Source HEAD changed')
    members, tracked = {}, []
    for source, name in FILES.items():
        entry = git('ls-tree', source_head, '--', source).decode().strip()
        if not entry or entry.split()[0:2] != ['100644', 'blob']:
            raise ValueError('Missing/non-regular committed example: ' + source)
        blob = entry.split()[2]
        path = root / source
        if (any(p.is_symlink() for p in (path, *path.parents) if p != root and root in p.parents)
                or not path.is_file() or path.stat().st_size > MAX_FILE):
            raise ValueError('Missing/symlinked/oversized example: ' + source)
        data = path.read_bytes()
        if len(data) > MAX_FILE or hashlib.sha1(
                b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() != blob:
            raise ValueError('Example differs from Git blob: ' + source)
        members[name] = data
        tracked.append({'path': name, 'sourcePath': source, 'gitBlobSha1': blob,
                        'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
    manifest = {'schemaVersion': 1, 'sourceHead': source_head, 'version': VERSION,
                'trackedMembers': sorted(tracked, key=lambda x: x['path']),
                'generatedMembers': ['EXAMPLES-MANIFEST.json'],
                'published': False, 'distributionAuthorized': False}
    members['EXAMPLES-MANIFEST.json'] = (json.dumps(manifest, sort_keys=True, indent=2) + '\n').encode()
    if sum(map(len, members.values())) > MAX_TOTAL:
        raise ValueError('Examples total budget exceeded')
    if git('rev-parse', 'HEAD').decode().strip() != source_head:
        raise ValueError('Source HEAD changed')
    output.mkdir(mode=0o700)
    archive = output / ARCHIVE
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name, data in sorted(members.items()):
            item = zipfile.ZipInfo('FactsPDF.Examples/' + name, (1980, 1, 1, 0, 0, 0))
            item.create_system = 3
            item.external_attr = 0o100644 << 16
            z.writestr(item, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
    if git('rev-parse', 'HEAD').decode().strip() != source_head:
        raise ValueError('Source HEAD changed after archive writing')
    report = {'schemaVersion': 1, 'phase': 'partial-examples-archive-unverified',
              'sourceHead': source_head, 'version': VERSION,
              'archive': {'filename': ARCHIVE, 'bytes': archive.stat().st_size,
                          'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()},
              'members': [{'path': p, 'bytes': len(b), 'sha256': hashlib.sha256(b).hexdigest()}
                          for p, b in sorted(members.items())],
              'candidateAssembled': False, 'independentlyValidated': False,
              'published': False, 'distributionAuthorized': False}
    (output / 'examples-archive.json').write_text(json.dumps(report, sort_keys=True, indent=2) + '\n')
    return report
