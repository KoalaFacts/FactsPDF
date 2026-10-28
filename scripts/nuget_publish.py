"""Fail-closed boundaries for the manual NuGet preview publisher (no credentials)."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.request import urlopen
from zipfile import ZipFile

REPOSITORY = 'KoalaFacts/FactsPDF'
VERSION_PATTERN = r'(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)-[0-9a-z-]+(?:\.[0-9a-z-]+)*'
BASE_MEMBERS = {'FactsPDF.nuspec', 'LICENSE.md', 'README.md',
                'lib/net10.0/FactsPDF.dll', '_rels/.rels', '[Content_Types].xml'}
REQUIRED_MEMBERS = {'FactsPDF.nuspec', 'LICENSE.md', 'README.md', 'lib/net10.0/FactsPDF.dll'}


def validate_request(*, declared: str, requested: str, event: str, ref: str,
                     publish: bool, approved: bool, source: str) -> str:
    version = requested or declared
    if not re.fullmatch(VERSION_PATTERN, version):
        raise ValueError('An exact lowercase prerelease version without build metadata is required')
    if any(part.isdigit() and len(part) > 1 and part.startswith('0')
           for part in version.split('-', 1)[1].split('.')):
        raise ValueError('Numeric prerelease identifiers cannot have leading zeroes')
    if version != declared:
        raise ValueError('Requested version differs from Directory.Build.props; do not relabel binaries')
    if not re.fullmatch(r'[0-9a-f]{40}', source):
        raise ValueError('An exact lowercase source commit SHA is required')
    if publish and (event != 'workflow_dispatch' or ref != 'refs/heads/main' or not approved):
        raise ValueError('Publishing requires a manual main run and explicit release-gate confirmation')
    return version


def verify_main_ci(pages: list[dict], source: str) -> int:
    runs = [run for page in pages for run in page.get('workflow_runs', [])
            if run.get('path') == '.github/workflows/ci.yml'
            and run.get('head_sha') == source and run.get('event') == 'push'
            and run.get('head_branch') == 'main'
            and run.get('head_repository', {}).get('full_name') == REPOSITORY]
    if not runs:
        raise ValueError('No exact-source main CI run exists; PR/old-source results are insufficient')
    latest = max(runs, key=lambda run: run['id'])
    if latest.get('status') != 'completed' or latest.get('conclusion') != 'success':
        raise ValueError('The latest exact-source main CI run is not successfully completed')
    return latest['id']


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def member_hashes(data: bytes, *, allow_signature: bool = False) -> dict[str, str]:
    with ZipFile(io.BytesIO(data)) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or not REQUIRED_MEMBERS.issubset(names):
            raise ValueError('Missing or duplicate NuGet package entries')
        result = {}
        for name in names:
            if name == '.signature.p7s' and allow_signature:
                continue  # NuGet.org may add its repository signature after upload.
            if name not in BASE_MEMBERS and not re.fullmatch(
                    r'package/services/metadata/core-properties/(?:nuget|[0-9a-f]+)\.psmdcp', name):
                raise ValueError('Unexpected package payload (no fonts, CLI, symbols or paths): ' + name)
            result[name] = digest(archive.read(name))
        return result


def seal(package: Path, bundle: Path, version: str, source: str) -> None:
    validate_request(declared=version, requested=version, event='workflow_dispatch',
                     ref='refs/heads/main', publish=False, approved=False, source=source)
    filename = f'FactsPDF.{version}.nupkg'
    if package.name != filename or not package.is_file() or package.is_symlink():
        raise ValueError('Expected one regular source-version FactsPDF NuGet package')
    if bundle.exists() or bundle.is_symlink():
        raise ValueError('Refusing to overwrite an existing package staging directory')
    data = package.read_bytes()
    members = member_hashes(data)
    manifest = dict(schemaVersion=1, packageId='FactsPDF', version=version, sourceCommit=source,
                    targetFramework='net10.0', filename=filename, sha256=digest(data), members=members)
    bundle.mkdir()
    (bundle / filename).write_bytes(data)
    (bundle / 'manifest.json').write_text(json.dumps(manifest, indent=2, sort_keys=True)+'\n', encoding='utf-8')


def verify_bundle(bundle: Path, version: str, source: str) -> Path:
    filename = f'FactsPDF.{version}.nupkg'
    if bundle.is_symlink() or not bundle.is_dir():
        raise ValueError('Expected a regular artifact directory')
    if {p.name for p in bundle.iterdir()} != {filename, 'manifest.json'}:
        raise ValueError('Artifact must contain exactly one NuGet package and its manifest')
    if any(p.is_symlink() or not p.is_file() for p in bundle.iterdir()):
        raise ValueError('Artifact entries must be regular files')
    manifest = json.loads((bundle / 'manifest.json').read_text(encoding='utf-8'))
    expected = dict(schemaVersion=1, packageId='FactsPDF', version=version, sourceCommit=source,
                    targetFramework='net10.0', filename=filename)
    if set(manifest) != set(expected) | {'sha256', 'members'} or any(
            manifest.get(key) != value for key, value in expected.items()):
        raise ValueError('Artifact identity differs from this workflow source/version')
    package = bundle / filename
    data = package.read_bytes()
    if digest(data) != manifest['sha256'] or member_hashes(data) != manifest['members']:
        raise ValueError('Artifact bytes differ from the tested package')
    return package


def verify_published(package: Path, manifest: dict) -> None:
    if member_hashes(package.read_bytes(), allow_signature=True) != manifest['members']:
        raise ValueError('NuGet.org package content differs from the verified upload')


def verify_public(bundle: Path, version: str, source: str, destination: Path) -> None:
    verify_bundle(bundle, version, source)
    manifest = json.loads((bundle / 'manifest.json').read_text(encoding='utf-8'))
    url = f'https://api.nuget.org/v3-flatcontainer/factspdf/{version}/factspdf.{version}.nupkg'
    if destination.exists() or destination.is_symlink():
        raise ValueError('Public verification requires a fresh temporary output path')
    for attempt in range(12):
        try:
            with urlopen(url, timeout=30) as response:
                data = response.read()
            with destination.open('xb') as stream:
                stream.write(data)
            verify_published(destination, manifest)
            print('Public NuGet package verified against the exact tested payload: ' + version)
            return
        except HTTPError as error:
            if error.code not in (404, 408, 429, 500, 502, 503, 504):
                raise
        except URLError:
            pass
        if attempt != 11:
            time.sleep(10)
    raise ValueError('Push may have succeeded, but public package availability is not yet verified')


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    request = sub.add_parser('request')
    request.add_argument('--version', default='')
    request.add_argument('--event', required=True)
    request.add_argument('--ref', required=True)
    request.add_argument('--publish', choices=('true', 'false'), required=True)
    request.add_argument('--approved', choices=('true', 'false'), required=True)
    request.add_argument('--source', required=True)
    gate = sub.add_parser('gate-ci')
    gate.add_argument('--runs', type=Path, required=True)
    gate.add_argument('--source', required=True)
    for name in ('seal', 'verify', 'verify-public'):
        command = sub.add_parser(name)
        command.add_argument('--bundle', type=Path, required=True)
        command.add_argument('--version', required=True)
        command.add_argument('--source', required=True)
        if name == 'seal':
            command.add_argument('--package', type=Path, required=True)
        if name == 'verify-public':
            command.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    if args.command == 'gate-ci':
        run = verify_main_ci(json.loads(args.runs.read_text(encoding='utf-8')), args.source)
        print('Exact-source main CI verified: ' + str(run))
        return
    # The existing repository verifier remains authoritative for source/metadata.
    # Imports stay here so pure boundary tests need neither .NET nor a checkout.
    from verify_package import current_source, expected_contract, validate_package
    root = Path(__file__).resolve().parents[1]
    declared, target = expected_contract(root)
    source = current_source(root)
    if source != args.source or target != 'net10.0':
        raise ValueError('Checkout/source or declared package target mismatch')
    version = validate_request(declared=declared, requested=args.version,
                               event=args.event if args.command == 'request' else 'workflow_dispatch',
                               ref=args.ref if args.command == 'request' else 'refs/heads/main',
                               publish=args.command == 'request' and args.publish == 'true',
                               approved=args.command == 'request' and args.approved == 'true', source=source)
    if args.command == 'request':
        print(f'PACKAGE_VERSION={version}\nSOURCE_SHA={source}')
    elif args.command == 'seal':
        validate_package(args.package, root, version, source, target)
        seal(args.package, args.bundle, version, source)
    else:
        package = verify_bundle(args.bundle, version, source)
        validate_package(package, root, version, source, target)
        if args.command == 'verify-public':
            verify_public(args.bundle, version, source, args.destination)
        else:
            print('Verified staged package source, version, licence, assets and exact bytes')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, AssertionError, OSError, KeyError) as error:
        print('NuGet publisher failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
