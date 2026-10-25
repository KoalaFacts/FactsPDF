"""Build the real, source-exact corresponding-source payload of the private preview.

This is one component of the five-payload local-Linux candidate, never a release
or an acceptance receipt. Only committed and byte-verified Git source is packed.
"""
from __future__ import annotations

import gzip
import hashlib
import io
import json
import posixpath
from pathlib import Path
import re
import subprocess
import tarfile
from xml.etree import ElementTree as ET

VERSION = '0.1.0-preview.1'
SOURCE_ARCHIVE = f'FactsPDF.{VERSION}.source.tar.gz'
SOURCE_ROOT = f'FactsPDF.{VERSION}.source'
MAX_MEMBER = 8 * 1024 * 1024
MAX_TOTAL = 64 * 1024 * 1024
MAX_FILES = 512
TOP_FILES = frozenset({
    'Directory.Build.props', 'global.json', 'LICENSE.md', 'README.md',
    'LICENSING.md', 'COMMERCIAL.md', 'CONTRIBUTING.md',
    'eng/delivery-candidate.json',
})
DOC_FILES = frozenset({
    'docs/development.md', 'docs/evaluation.md', 'docs/combined-report.md',
    'docs/tables.md', 'docs/images.md', 'docs/font-styles.md',
    'docs/page-furniture-cli.md', 'docs/page-margins.md',
    'docs/unicode-fonts.md', 'docs/font-subsetting.md',
})
SCRIPT_FILES = frozenset({
    'scripts/inspect_candidate_assembly.cs',
    'scripts/verify_package.py', 'scripts/verify_package_inspector.py',
    'scripts/verify_release_candidate.py', 'scripts/verify_first_use.py',
    'scripts/stage_delivery_candidate.py', 'scripts/private_source_payload.py',
})
REQUIRED = frozenset({
    'Directory.Build.props', 'global.json', 'LICENSE.md', 'README.md',
    'eng/delivery-candidate.json',
    'src/FactsPDF/FactsPDF.csproj',
    'src/FactsPDF.Cli/FactsPDF.Cli.csproj',
    'shared/PageFurnitureJson.cs',
    'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj',
    'scripts/inspect_candidate_assembly.cs',
    'scripts/private_source_payload.py',
})


def _git(root: Path, *args: str) -> bytes:
    return subprocess.check_output(['git', *args], cwd=root)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _include(path: str) -> bool:
    if path in TOP_FILES | DOC_FILES | SCRIPT_FILES:
        return True
    if path.startswith(('src/', 'shared/')):
        return path.endswith(('.cs', '.csproj', '.props', '.targets', '.resx'))
    if path.startswith('tools/FactsPDF.PackageInspector/'):
        return path.endswith(('.cs', '.csproj', '.props', '.targets'))
    if path.startswith('examples/'):
        return path.endswith(('.html', '.json', '.cs', '.csproj', '/NuGet.Config'))
    return False


def _tar_entry(name: str, payload: bytes) -> tuple[tarfile.TarInfo, io.BytesIO]:
    info = tarfile.TarInfo(name)
    info.type = tarfile.REGTYPE
    info.size = len(payload)
    info.mode = 0o644
    info.uid = info.gid = info.mtime = 0
    info.uname = info.gname = ''
    return info, io.BytesIO(payload)


def assemble_source(root: Path, output: Path, source_head: str) -> dict:
    """Create a bounded corresponding-source tarball bound to a committed SHA.

    The caller must enforce clean checkout using verify_package.current_source.
    No byte is accepted from the checkout without matching its Git blob object.
    """
    root = root.resolve(strict=True)
    output = output.absolute()
    dest = output.resolve()
    if (not re.fullmatch(r'[0-9a-f]{40}', source_head) or
            dest == root or root in dest.parents or output.exists() or
            output.is_symlink() or not output.parent.is_dir()):
        raise ValueError('Fresh external source archive directory and exact source SHA required')
    if _git(root, 'rev-parse', 'HEAD').decode().strip() != source_head:
        raise ValueError('Source HEAD changed before source packaging')
    members: dict[str, bytes] = {}
    tracked: list[dict] = []
    raw_tree = _git(root, 'ls-tree', '-r', '-z', '--full-tree', source_head)
    for record in filter(None, raw_tree.split(b'\x00')):
        header, raw_path = record.split(b'\t', 1)
        mode, kind, blob = header.split()
        path = raw_path.decode('utf-8')
        if not _include(path):
            continue
        if (not re.fullmatch(r'[A-Za-z0-9._/-]+', path) or
                any(p in ('.', '..', '') for p in path.split('/'))):
            raise ValueError('Unsafe source path: ' + path)
        if mode != b'100644' or kind != b'blob' or len(blob) != 40:
            raise ValueError('Non-regular tracked source member: ' + path)
        local = root / path
        if local.is_symlink() or not local.is_file():
            raise ValueError('Missing or symlinked tracked source: ' + path)
        length = local.stat().st_size
        if length > MAX_MEMBER:
            raise ValueError('Source member exceeds bound: ' + path)
        data = local.read_bytes()
        real_blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\x00' + data).hexdigest()
        if len(data) != length or real_blob != blob.decode():
            raise ValueError('Source differs from committed Git blob: ' + path)
        members[path] = data
        tracked.append({'path': path, 'gitBlobSha1': real_blob,
                        'bytes': len(data), 'sha256': _sha256(data)})
        if len(members) > MAX_FILES or sum(len(x) for x in members.values()) > MAX_TOTAL:
            raise ValueError('Source archive entry or total budget exceeded')
    if not REQUIRED.issubset(members):
        raise ValueError('Missing required source/build closure: ' +
                         ', '.join(sorted(REQUIRED - members.keys())))
    # Explicit Compile/ProjectReference/None file links must survive the archive.
    # The SDK's implicit source globs are covered by selecting all src/ and shared/ .cs.
    for project in ('src/FactsPDF/FactsPDF.csproj',
                    'src/FactsPDF.Cli/FactsPDF.Cli.csproj',
                    'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj'):
        doc = ET.fromstring(members[project])
        for tag in ('Compile', 'ProjectReference', 'None'):
            for item in doc.findall('.//' + tag):
                link = item.attrib.get('Include')
                if not link:
                    continue
                if any(ch in link for ch in '$*?\\'):
                    raise ValueError('Unreviewed source project link: ' + project)
                resolved = posixpath.normpath(posixpath.join(posixpath.dirname(project), link))
                if resolved not in members:
                    raise ValueError('Missing linked source/build material: ' + resolved)
        if doc.findall('.//Import'):
            raise ValueError('Unreviewed explicit source-project import: ' + project)
    tracked.sort(key=lambda x: x['path'])
    manifest = {'schemaVersion': 1, 'packageId': 'FactsPDF',
                'packageVersion': VERSION, 'sourceHead': source_head,
                'trackedMembers': tracked,
                'generatedMembers': ['SOURCE-BUILD.md', 'SOURCE-MANIFEST.json'],
                'published': False, 'distributionAuthorized': False}
    generated_manifest = (json.dumps(manifest, sort_keys=True, indent=2) + '\n').encode()
    recipe = (f'# FactsPDF {VERSION} — corresponding source (private preview)\n\n'
              f'Git source commit: {source_head}. This directory has no .git.\n'
              'It contains tracked source, the linked furniture decoder, and exact\n'
              'build inputs. No fonts, customer documents or prebuilt PDFs.\n\n'
              'With the official .NET SDK 10.0.402 and runtime 10.0.13 installed:\n\n'
              '    dotnet build src/FactsPDF.Cli/FactsPDF.Cli.csproj -c Release '
              f'-p:SourceRevisionId={source_head} -p:ContinuousIntegrationBuild=true\n\n'
              'This source archive is not a verified installable candidate, an\n'
              'independent binary attestation, or a grant to distribute software.\n'
              'See LICENSE.md and LICENSING.md before use.\n').encode()
    members['SOURCE-MANIFEST.json'] = generated_manifest
    members['SOURCE-BUILD.md'] = recipe
    total = sum(len(x) for x in members.values())
    if total > MAX_TOTAL or len(members) > MAX_FILES:
        raise ValueError('Generated source archive budget exceeded')
    if _git(root, 'rev-parse', 'HEAD').decode().strip() != source_head:
        raise ValueError('Source HEAD changed before archive writing')
    output.mkdir(mode=0o700)
    archive = output / SOURCE_ARCHIVE
    with archive.open('wb') as stream:
        with gzip.GzipFile(fileobj=stream, mode='wb', filename='', mtime=0,
                           compresslevel=9) as compressed:
            with tarfile.open(fileobj=compressed, mode='w', format=tarfile.USTAR_FORMAT) as tar:
                root_info = tarfile.TarInfo(SOURCE_ROOT + '/')
                root_info.type = tarfile.DIRTYPE
                root_info.mode = 0o755
                root_info.uid = root_info.gid = root_info.mtime = 0
                tar.addfile(root_info)
                for path, data in sorted(members.items()):
                    info, buffer = _tar_entry(SOURCE_ROOT + '/' + path, data)
                    tar.addfile(info, buffer)
    if archive.stat().st_size > MAX_TOTAL or _git(root, 'rev-parse', 'HEAD').decode().strip() != source_head:
        raise ValueError('Source archive budget/source changed')
    report = {'schemaVersion': 1, 'phase': 'partial-source-archive-unverified',
              'sourceHead': source_head, 'version': VERSION,
              'archive': {'filename': SOURCE_ARCHIVE, 'bytes': archive.stat().st_size,
                          'sha256': _sha256(archive.read_bytes())},
              'members': [{'path': p, 'bytes': len(data), 'sha256': _sha256(data),
                           'kind': 'generated' if p.startswith('SOURCE-') else 'git-blob'}
                          for p, data in sorted(members.items())],
              'candidateAssembled': False, 'independentlyValidated': False,
              'published': False, 'distributionAuthorized': False}
    (output / 'source-archive.json').write_text(json.dumps(report, sort_keys=True, indent=2) + '\n')
    return report
