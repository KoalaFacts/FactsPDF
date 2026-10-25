"""Source-clean, bounded preflight for the future private FactsPDF candidate producer.

This phase creates only a source-inspection receipt. It builds no package, native
binary or installable candidate, and never uploads/publishes software.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from xml.etree import ElementTree as ET

from verify_package import current_source, expected_contract

VERSION = '0.1.0-preview.1'
TARGET = 'net10.0'
SDK = '10.0.402'
RUNTIME = '10.0.13'
PROFILES = {'local-linux': 'linux-x64', 'win-x64-validation': 'win-x64'}
CONFIG_NAMES = ('Directory.Build.props', 'Directory.Build.targets',
                'Directory.Packages.props', 'Directory.Build.rsp')
INSPECTOR = Path('tools/FactsPDF.PackageInspector')
LINK = Path('scripts/inspect_candidate_assembly.cs')


def _git(root: Path, *args: str) -> bytes:
    return subprocess.check_output(['git', *args], cwd=root)


def _sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def identity(root: Path, profile: str) -> dict:
    if profile not in PROFILES:
        raise ValueError('Unknown delivery profile')
    version, target = expected_contract(root)
    if (version, target) != (VERSION, TARGET):
        raise ValueError('Fixed candidate source version/target mismatch')
    sdk = json.loads((root / 'global.json').read_text(encoding='utf-8'))
    if sdk != {'sdk': {'version': SDK, 'rollForward': 'disable', 'allowPrerelease': False}}:
        raise ValueError('Fixed candidate SDK roll-forward mismatch')
    data = json.loads((root / 'eng/delivery-candidate.json').read_text(encoding='utf-8'))
    fields = {'schemaVersion': 1, 'packageId': 'FactsPDF', 'version': VERSION,
              'targetFramework': TARGET}
    if any(data.get(k) != v for k, v in fields.items()):
        raise ValueError('Candidate identity declaration mismatch')
    selected_sdk = data.get('sdk', {})
    if any(selected_sdk.get(k) != v for k, v in sdk['sdk'].items()):
        raise ValueError('Candidate SDK declaration mismatch')
    if data.get('managedRuntime') != {'framework': 'Microsoft.NETCore.App',
                                      'version': RUNTIME, 'rollForward': 'Disable'}:
        raise ValueError('Candidate managed-runtime declaration mismatch')
    if data.get('nativePacks') != {'runtimeVersion': RUNTIME, 'compilerVersion': RUNTIME}:
        raise ValueError('Candidate native-pack declaration mismatch')
    if data.get('publication') != {'published': False, 'distributionAuthorized': False}:
        raise ValueError('Candidate is not authorized for distribution')
    profiles = data.get('profiles')
    if not isinstance(profiles, list) or {x.get('name'): (x.get('rid'), x.get('binaryRetained'))
                                                 for x in profiles if isinstance(x, dict)} != {
            'local-linux': ('linux-x64', True), 'win-x64-validation': ('win-x64', False)} or len(profiles) != 2:
        raise ValueError('Candidate platform/retention mismatch')
    return {'packageId': 'FactsPDF', 'version': VERSION, 'targetFramework': TARGET,
            'sdkVersion': SDK, 'managedRuntimeVersion': RUNTIME,
            'nativeRuntimePackVersion': RUNTIME, 'nativeCompilerPackVersion': RUNTIME,
            'profile': profile, 'rid': PROFILES[profile],
            'binaryRetained': profile == 'local-linux'}


def inspector_inputs(root: Path) -> list[dict]:
    """Freeze actual SDK implicit source/resource globs plus explicit link/import closure.

    The inspector is a build-time metadata oracle; ignored sources under its own
    default glob must never become trusted merely because git status is clean.
    """
    tracked = set(filter(None, _git(root, 'ls-files', '-z').decode().split('\0')))
    project = root / INSPECTOR
    doc = ET.parse(project / 'FactsPDF.PackageInspector.csproj').getroot()
    if doc.attrib != {'Sdk': 'Microsoft.NET.Sdk'}:
        raise ValueError('Unexpected inspector SDK or project import')
    if doc.findall('.//Import') or doc.findall('.//ProjectReference') or doc.findall('.//EmbeddedResource'):
        raise ValueError('Unexpected inspector imports or explicit resources')
    if any(node.attrib for node in doc if node.tag in ('PropertyGroup', 'ItemGroup')):
        raise ValueError('Conditional inspector build group')
    if any(node.tag not in ('PropertyGroup', 'ItemGroup') for node in doc):
        raise ValueError('Unexpected inspector build target or project node')
    if any(node.tag not in ('OutputType', 'IsPackable') for group in doc.findall('PropertyGroup') for node in group):
        raise ValueError('Unexpected inspector build property')
    if any(node.tag != 'Compile' for group in doc.findall('ItemGroup') for node in group):
        raise ValueError('Unexpected inspector build item')
    if any(node.attrib for group in doc for node in group if node.tag != 'Compile'):
        raise ValueError('Conditional inspector build item/property')
    explicit = doc.findall('.//Compile')
    if len(explicit) != 1 or explicit[0].attrib != {
            'Include': '../../scripts/inspect_candidate_assembly.cs', 'Link': 'Program.cs'}:
        raise ValueError('Unexpected inspector source-link closure')
    if any(node.tag in ('EnableDefaultCompileItems', 'DefaultItemExcludes',
                        'DefaultItemExcludesInProjectFolder', 'Compile') and node is not explicit[0]
           for node in doc.iter()):
        raise ValueError('Unexpected inspector glob override')
    selected = {INSPECTOR / 'FactsPDF.PackageInspector.csproj', LINK}
    for directory in (Path('.'), Path('tools'), INSPECTOR):
        selected.update(directory / name for name in CONFIG_NAMES
                        if (root / directory / name).exists() or (root / directory / name).is_symlink())
    # SDK default Compile/**.cs and EmbeddedResource/**.resx, excluding only
    # conventional top-level build outputs. Do not ban unrelated ignored shared/*.
    for candidate in project.rglob('*'):
        relative = candidate.relative_to(project)
        if relative.parts[0] in ('bin', 'obj'):
            continue
        if candidate.is_symlink():
            raise ValueError('Symlink in inspector project inputs')
        if candidate.is_file() and candidate.suffix.lower() in ('.cs', '.resx', '.props', '.targets', '.rsp', '.csproj'):
            selected.add(INSPECTOR / relative)
    result = []
    for rel in sorted(selected, key=lambda p: p.as_posix()):
        name = rel.as_posix()
        if name.startswith('./'):
            name = name[2:]
        path = root / rel
        if name not in tracked or not path.is_file() or path.is_symlink():
            raise ValueError('Untracked/ignored/missing inspector input: ' + name)
        if name.endswith(('.props', '.targets')) and ET.fromstring(path.read_bytes()).findall('.//Import'):
            raise ValueError('Unreviewed inspector configuration import: ' + name)
        raw = path.read_bytes()
        result.append({'path': name, 'bytes': len(raw), 'sha256': _sha(raw)})
    return result


def preflight(root: Path, profile: str, output: Path) -> dict:
    root = root.resolve(strict=True)
    output = output.absolute()
    # No existing output can be overwritten; staging must stay outside checkout.
    resolved = output.resolve()
    if resolved == root or root in resolved.parents or output.exists() or output.is_symlink():
        raise ValueError('Fresh external candidate preflight directory required')
    if not output.parent.is_dir():
        raise ValueError('Output parent must already exist')
    source = current_source(root)
    if not re.fullmatch(r'[0-9a-f]{40}', source):
        raise ValueError('Expected exact Git source SHA')
    selected = identity(root, profile)
    inputs = inspector_inputs(root)
    if current_source(root) != source or inspector_inputs(root) != inputs:
        raise ValueError('Source or inspector inputs changed during preflight')
    receipt = {'schemaVersion': 1, 'phase': 'source-preflight', 'sourceHead': source,
               'identity': selected, 'inspectorInputs': inputs,
               'candidateBuilt': False, 'independentlyValidated': False,
               'published': False, 'distributionAuthorized': False}
    output.mkdir(mode=0o700)
    (output / 'source-preflight.json').write_text(
        json.dumps(receipt, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    return receipt


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    check = commands.add_parser('preflight', help='Validate candidate inputs without building software')
    check.add_argument('--profile', required=True, choices=sorted(PROFILES))
    check.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    receipt = preflight(root, args.profile, args.output)
    print(f"Preflight: {receipt['sourceHead']} / {args.profile}; no binary or candidate built.")


if __name__ == '__main__':
    main()
